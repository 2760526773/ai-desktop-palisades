using Palisades.Helpers;
using Palisades.Model;
using Palisades.View;
using Palisades.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;

namespace Palisades
{
    internal sealed class AutoOrganizeResult
    {
        public int CandidateCount { get; set; }
        public int AddedShortcutCount { get; set; }
        public int GroupCount { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    internal sealed class AiClassifyCache
    {
        public string Fingerprint { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    internal sealed class AutoOrganizePlan
    {
        public int CandidateCount { get; set; }
        public int GroupCount { get; set; }
        public string Fingerprint { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public Dictionary<string, List<string>> Grouped { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool CacheHit { get; set; }
    }

    internal static class PalisadesManager
    {
        public static readonly Dictionary<string, Palisade> palisades = new();
        private static readonly HashSet<string> AllowedCategories = new(new[] { "游戏", "影音", "娱乐", "文档", "开发", "社交", "工具", "系统", "其他" }, StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim OrganizeLock = new(1, 1);
        private const string ClassifierVersion = "2026-03-08-physical-archive-v2";

        public static void LoadPalisades()
        {
            string saveDirectory = PDirectory.GetPalisadesDirectory();
            PDirectory.EnsureExists(saveDirectory);

            List<PalisadeModel> loadedModels = new();
            foreach (string palisadeDir in Directory.GetDirectories(saveDirectory))
            {
                string statePath = Path.Combine(palisadeDir, "state.xml");
                if (!File.Exists(statePath))
                {
                    continue;
                }

                XmlSerializer deserializer = new(typeof(PalisadeModel));
                using StreamReader reader = new(statePath);
                if (deserializer.Deserialize(reader) is not PalisadeModel model)
                {
                    continue;
                }

                model.Shortcuts ??= new ObservableCollection<Shortcut>();
                if (IsGarbledName(model.Name) && model.Shortcuts.Count == 0)
                {
                    continue;
                }

                if (IsGarbledName(model.Name))
                {
                    model.Name = "其他";
                }

                model.Shortcuts = DeduplicateShortcutCollection(model.Shortcuts);
                MigrateLegacyVisual(model);
                ClampFenceBounds(model);
                loadedModels.Add(model);
            }

            foreach (PalisadeModel loadedModel in loadedModels)
            {
                palisades[loadedModel.Identifier] = new Palisade(new PalisadeViewModel(loadedModel));
            }
        }

        public static AutoOrganizePlan PrepareAutoOrganizeDesktopByAi()
        {
            OrganizeLock.Wait();
            try
            {
                Helpers.AiSettings settings = Helpers.AiSettings.Load();
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!Directory.Exists(desktop))
                {
                    return new AutoOrganizePlan { StatusMessage = "桌面目录不存在。" };
                }

                List<DesktopCandidate> candidates = BuildDesktopCandidates(desktop);
                if (candidates.Count == 0)
                {
                    return new AutoOrganizePlan { StatusMessage = "桌面未发现可分类项目。" };
                }

                string fingerprint = ComputeFingerprint(candidates, settings);
                AiClassifyCache cache = LoadClassifyCache();
                if (!string.IsNullOrWhiteSpace(cache.Fingerprint) && string.Equals(cache.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return new AutoOrganizePlan
                    {
                        CandidateCount = candidates.Count,
                        Fingerprint = fingerprint,
                        CacheHit = true,
                        StatusMessage = "桌面未变化，跳过 AI 分类（缓存命中）。"
                    };
                }

                Dictionary<string, string> classified = AiClassifier.Classify(candidates, settings);
                Dictionary<string, List<string>> grouped = new(StringComparer.OrdinalIgnoreCase);
                foreach (DesktopCandidate candidate in candidates)
                {
                    string raw = classified.TryGetValue(candidate.Path, out string? value) ? value : "其他";
                    string category = NormalizeCategory(raw);
                    if (!grouped.ContainsKey(category))
                    {
                        grouped[category] = new List<string>();
                    }
                    grouped[category].Add(candidate.Path);
                }

                string aiMsg = string.IsNullOrWhiteSpace(AiClassifier.LastRunInfo.Message) ? string.Empty : $"\n{AiClassifier.LastRunInfo.Message}";
                return new AutoOrganizePlan
                {
                    CandidateCount = candidates.Count,
                    GroupCount = grouped.Count,
                    Fingerprint = fingerprint,
                    Grouped = grouped,
                    StatusMessage = $"扫描 {candidates.Count} 项，分类 {grouped.Count} 组。{aiMsg}".Trim()
                };
            }
            catch (Exception ex)
            {
                return new AutoOrganizePlan { StatusMessage = $"整理失败：{ex.Message}" };
            }
            finally
            {
                OrganizeLock.Release();
            }
        }

        public static AutoOrganizeResult ApplyAutoOrganizePlan(AutoOrganizePlan plan)
        {
            AutoOrganizeResult result = new()
            {
                CandidateCount = plan.CandidateCount,
                GroupCount = plan.GroupCount,
                StatusMessage = plan.StatusMessage,
            };

            if (plan.CacheHit || plan.Grouped.Count == 0)
            {
                return result;
            }

            ResetManagedCategoryFences();

            int added = 0;
            foreach (var group in plan.Grouped)
            {
                PalisadeViewModel fence = EnsureFenceByName(group.Key);
                foreach (string sourcePath in group.Value)
                {
                    Shortcut? shortcut = MovePathIntoFenceAndBuildShortcut(sourcePath, fence);
                    if (shortcut == null)
                    {
                        continue;
                    }

                    fence.Shortcuts.Add(shortcut);
                    added++;
                }

                DeduplicateShortcutsInFence(fence);
                RemoveMissingShortcutsFromFence(fence);
                fence.Save();
            }

            SaveClassifyCache(new AiClassifyCache
            {
                Fingerprint = plan.Fingerprint,
                UpdatedAtUtc = DateTime.UtcNow,
            });

            result.AddedShortcutCount = added;
            result.StatusMessage = $"{plan.StatusMessage}\n已物理归档 {added} 项。".Trim();
            return result;
        }

        public static Shortcut? MovePathIntoFenceAndBuildShortcut(string sourcePath, PalisadeViewModel fence)
        {
            if (fence == null || string.IsNullOrWhiteSpace(sourcePath))
            {
                return null;
            }

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                return null;
            }

            string normalizedSource = NormalizePath(sourcePath);
            string targetDirectory = PDirectory.GetManagedCategoryDirectory(fence.Name);
            string targetPath = MovePathToDirectory(normalizedSource, targetDirectory);

            RemoveShortcutFromAllFences(normalizedSource);
            RemoveShortcutFromAllFences(targetPath);

            Shortcut? shortcut = BuildShortcut(targetPath, fence.Identifier);
            return shortcut;
        }

        public static bool TryMoveShortcutToDesktop(Shortcut shortcut, out string movedPath, out string message)
        {
            movedPath = string.Empty;
            message = string.Empty;

            if (shortcut == null)
            {
                message = "未找到项目。";
                return false;
            }

            string sourcePath = shortcut.UriOrFileAction;
            if (!ShortcutTargetExists(sourcePath))
            {
                RemoveShortcutFromAllFences(sourcePath);
                message = "源项目不存在，已清理栅栏引用。";
                return false;
            }

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                movedPath = MovePathToDirectory(sourcePath, desktop);
                RemoveShortcutFromAllFences(sourcePath);
                RemoveMissingShortcutsFromAllFences();
                message = $"已移回桌面：{Path.GetFileName(movedPath)}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"移回桌面失败：{ex.Message}";
                return false;
            }
        }

        public static bool IsManagedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fullPath = NormalizePath(path);
            string root = NormalizeDirectoryPath(PDirectory.GetManagedItemsRootDirectory());
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        public static void RemoveShortcutFromAllFences(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string normalized = NormalizePath(path);
            foreach (Palisade p in palisades.Values)
            {
                if (p.DataContext is not PalisadeViewModel vm)
                {
                    continue;
                }

                List<Shortcut> remove = vm.Shortcuts.Where(s => string.Equals(NormalizePath(s.UriOrFileAction), normalized, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (Shortcut s in remove)
                {
                    vm.Shortcuts.Remove(s);
                }
                if (remove.Count > 0)
                {
                    vm.Save();
                }
            }
        }

        public static void RemoveMissingShortcutsFromAllFences()
        {
            foreach (Palisade p in palisades.Values)
            {
                if (p.DataContext is PalisadeViewModel vm)
                {
                    RemoveMissingShortcutsFromFence(vm);
                }
            }
        }

        public static void RemoveMissingShortcutsFromFence(PalisadeViewModel fence)
        {
            List<Shortcut> remove = fence.Shortcuts.Where(s => !ShortcutTargetExists(s.UriOrFileAction)).ToList();
            foreach (Shortcut s in remove)
            {
                fence.Shortcuts.Remove(s);
            }
            if (remove.Count > 0)
            {
                fence.Save();
            }
        }

        private static string MovePathToDirectory(string sourcePath, string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return sourcePath;
            }

            sourcePath = NormalizePath(sourcePath);
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                return sourcePath;
            }

            PDirectory.EnsureExists(targetDirectory);
            string normalizedTargetDir = NormalizeDirectoryPath(targetDirectory);
            string currentParent = NormalizeDirectoryPath(Path.GetDirectoryName(sourcePath) ?? string.Empty);
            if (string.Equals(currentParent, normalizedTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                return sourcePath;
            }

            string name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string destination = GetUniquePath(Path.Combine(targetDirectory, name));

            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destination);
                return NormalizePath(destination);
            }

            File.Move(sourcePath, destination);
            return NormalizePath(destination);
        }

        private static string GetUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return path;
            }

            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int index = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, $"{fileName} ({index}){extension}");
                index++;
            }
            while (File.Exists(candidate) || Directory.Exists(candidate));

            return candidate;
        }

        private static bool ShortcutTargetExists(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                return false;
            }

            string normalized = NormalizePath(action);
            return File.Exists(normalized) || Directory.Exists(normalized);
        }

        private static void ResetManagedCategoryFences()
        {
            foreach (Palisade p in palisades.Values)
            {
                if (p.DataContext is not PalisadeViewModel vm)
                {
                    continue;
                }
                if (!AllowedCategories.Contains(vm.Name))
                {
                    continue;
                }
                if (vm.Shortcuts.Count == 0)
                {
                    continue;
                }

                vm.Shortcuts.Clear();
                vm.Save();
            }
        }

        private static List<DesktopCandidate> BuildDesktopCandidates(string desktop)
        {
            List<DesktopCandidate> list = new();
            int idCounter = 1;

            foreach (string file in Directory.GetFiles(desktop, "*", SearchOption.TopDirectoryOnly))
            {
                if (ShouldSkip(file))
                {
                    continue;
                }

                string ext = Path.GetExtension(file);
                string name = Path.GetFileNameWithoutExtension(file);
                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string? target = LnkShortcut.TryResolveTargetPath(file);
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        string targetHint = Path.GetFileNameWithoutExtension(target);
                        if (!string.IsNullOrWhiteSpace(targetHint))
                        {
                            name = $"{name} {targetHint}";
                        }
                    }
                }

                list.Add(new DesktopCandidate
                {
                    Path = file,
                    Name = name,
                    DisplayName = Path.GetFileName(file),
                    Extension = ext,
                    IsDirectory = false,
                    Id = idCounter++,
                });
            }

            foreach (string dir in Directory.GetDirectories(desktop, "*", SearchOption.TopDirectoryOnly))
            {
                if (ShouldSkip(dir))
                {
                    continue;
                }

                list.Add(new DesktopCandidate
                {
                    Path = dir,
                    Name = Path.GetFileName(dir),
                    DisplayName = Path.GetFileName(dir),
                    Extension = "[dir]",
                    IsDirectory = true,
                    Id = idCounter++,
                });
            }

            return list;
        }

        private static bool ShouldSkip(string path)
        {
            string name = Path.GetFileName(path);
            if (string.Equals(name, "desktop.ini", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                FileAttributes attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.Hidden) != 0 || (attrs & FileAttributes.System) != 0)
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static string NormalizeCategory(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "其他";
            }

            string trimmed = raw.Trim().Replace("\"", string.Empty);
            if (AllowedCategories.Contains(trimmed)) return trimmed;

            string lower = trimmed.ToLowerInvariant();
            if (lower.Contains("game") || lower.Contains("游戏")) return "游戏";
            if (lower.Contains("video") || lower.Contains("media") || lower.Contains("影音") || lower.Contains("music")) return "影音";
            if (lower.Contains("entertain") || lower.Contains("娱乐")) return "娱乐";
            if (lower.Contains("doc") || lower.Contains("office") || lower.Contains("pdf") || lower.Contains("文档")) return "文档";
            if (lower.Contains("dev") || lower.Contains("code") || lower.Contains("开发") || lower.Contains("program")) return "开发";
            if (lower.Contains("social") || lower.Contains("chat") || lower.Contains("社交")) return "社交";
            if (lower.Contains("tool") || lower.Contains("util") || lower.Contains("工具")) return "工具";
            if (lower.Contains("system") || lower.Contains("driver") || lower.Contains("系统")) return "系统";
            return "其他";
        }

        private static Shortcut? BuildShortcut(string path, string palisadeIdentifier)
        {
            if (Directory.Exists(path))
            {
                return FileShortcut.BuildFrom(path, palisadeIdentifier);
            }

            string ext = Path.GetExtension(path);
            if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                Shortcut? lnk = LnkShortcut.BuildFrom(path, palisadeIdentifier);
                return lnk ?? FileShortcut.BuildFrom(path, palisadeIdentifier);
            }
            if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                Shortcut? url = UrlShortcut.BuildFrom(path, palisadeIdentifier);
                return url ?? FileShortcut.BuildFrom(path, palisadeIdentifier);
            }
            return FileShortcut.BuildFrom(path, palisadeIdentifier);
        }

        private static PalisadeViewModel EnsureFenceByName(string name)
        {
            foreach (var pair in palisades)
            {
                if (pair.Value.DataContext is PalisadeViewModel vm && vm.Name == name)
                {
                    return vm;
                }
            }

            PalisadeViewModel created = new() { Name = name };
            ApplyDefaultFenceLayout(created, palisades.Count);
            palisades[created.Identifier] = new Palisade(created);
            created.Save();
            return created;
        }

        private static void ApplyDefaultFenceLayout(PalisadeViewModel created, int index)
        {
            const int width = 520;
            const int height = 340;
            const int gap = 16;

            double screenW = SystemParameters.PrimaryScreenWidth;
            int cols = Math.Max(1, (int)((screenW - gap) / (width + gap)));
            int col = index % cols;
            int row = index / cols;

            created.Width = width;
            created.Height = height;
            created.FenceX = gap + col * (width + gap);
            created.FenceY = 50 + row * (height + gap);
        }

        private static bool IsGarbledName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            string trimmed = name.Trim();
            if (trimmed == "?" || trimmed == "??" || trimmed == "???") return true;
            return trimmed.All(c => c == '?');
        }

        private static void MigrateLegacyVisual(PalisadeModel model)
        {
            Color oldHeader = Color.FromArgb(200, 0, 0, 0);
            Color oldBody = Color.FromArgb(120, 0, 0, 0);
            if (model.HeaderColor == oldHeader && model.BodyColor == oldBody)
            {
                model.HeaderColor = Color.FromArgb(190, 255, 255, 255);
                model.BodyColor = Color.FromArgb(96, 230, 240, 255);
                model.TitleColor = Color.FromArgb(255, 33, 37, 41);
                model.LabelsColor = Color.FromArgb(255, 33, 37, 41);
            }
        }

        private static void ClampFenceBounds(PalisadeModel model)
        {
            model.Width = Math.Clamp(model.Width, 320, 680);
            model.Height = Math.Clamp(model.Height, 220, 520);
            int maxX = Math.Max(0, (int)SystemParameters.PrimaryScreenWidth - model.Width);
            int maxY = Math.Max(0, (int)SystemParameters.PrimaryScreenHeight - model.Height);
            model.FenceX = Math.Clamp(model.FenceX, 0, maxX);
            model.FenceY = Math.Clamp(model.FenceY, 0, maxY);
        }

        private static string BuildShortcutKey(Shortcut shortcut)
        {
            string action = shortcut.UriOrFileAction?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(action)) return NormalizePath(action).ToLowerInvariant();
            string name = shortcut.Name?.Trim() ?? string.Empty;
            return $"name:{name.ToLowerInvariant()}";
        }

        private static ObservableCollection<Shortcut> DeduplicateShortcutCollection(ObservableCollection<Shortcut> shortcuts)
        {
            ObservableCollection<Shortcut> clean = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (Shortcut s in shortcuts)
            {
                string key = BuildShortcutKey(s);
                if (!seen.Add(key)) continue;
                clean.Add(s);
            }
            return clean;
        }

        private static void DeduplicateShortcutsInFence(PalisadeViewModel fence)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<Shortcut> remove = new();
            foreach (Shortcut s in fence.Shortcuts)
            {
                string key = BuildShortcutKey(s);
                if (!seen.Add(key)) remove.Add(s);
            }
            foreach (Shortcut s in remove) fence.Shortcuts.Remove(s);
        }

        private static string GetCachePath()
        {
            return Path.Combine(PDirectory.GetAppDirectory(), "ai-classify-cache.json");
        }

        private static AiClassifyCache LoadClassifyCache()
        {
            string path = GetCachePath();
            if (!File.Exists(path)) return new AiClassifyCache();
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AiClassifyCache>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AiClassifyCache();
            }
            catch
            {
                return new AiClassifyCache();
            }
        }

        private static void SaveClassifyCache(AiClassifyCache cache)
        {
            string dir = PDirectory.GetAppDirectory();
            PDirectory.EnsureExists(dir);
            string path = GetCachePath();
            string json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static string ComputeFingerprint(List<DesktopCandidate> candidates, Helpers.AiSettings settings)
        {
            StringBuilder sb = new();
            sb.Append(ClassifierVersion).Append('|');
            sb.Append(settings.Provider).Append('|');
            if (settings.Providers.TryGetValue(settings.Provider, out ProviderSettings? p)) sb.Append(p.Model).Append('|');

            foreach (DesktopCandidate c in candidates.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            {
                DateTime lastWrite = DateTime.MinValue;
                long size = 0;
                try
                {
                    if (c.IsDirectory)
                    {
                        DirectoryInfo di = new(c.Path);
                        lastWrite = di.Exists ? di.LastWriteTimeUtc : DateTime.MinValue;
                    }
                    else
                    {
                        FileInfo fi = new(c.Path);
                        if (fi.Exists)
                        {
                            lastWrite = fi.LastWriteTimeUtc;
                            size = fi.Length;
                        }
                    }
                }
                catch
                {
                }

                sb.Append(c.Path).Append('|').Append(c.Extension).Append('|').Append(c.IsDirectory ? 'D' : 'F').Append('|').Append(lastWrite.Ticks).Append('|').Append(size).Append(';');
            }

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return NormalizePath(path) + Path.DirectorySeparatorChar;
        }

        public static void CreatePalisade()
        {
            PalisadeViewModel viewModel = new();
            ApplyDefaultFenceLayout(viewModel, palisades.Count);
            palisades[viewModel.Identifier] = new Palisade(viewModel);
            viewModel.Save();
        }

        public static void DeletePalisade(string identifier)
        {
            palisades.TryGetValue(identifier, out Palisade? palisade);
            if (palisade == null) return;
            if (palisade.DataContext is PalisadeViewModel vm) vm.Delete();
            palisade.Close();
            palisades.Remove(identifier);
        }

        public static Palisade GetPalisade(string identifier)
        {
            palisades.TryGetValue(identifier, out Palisade? palisade);
            if (palisade == null) throw new KeyNotFoundException(identifier);
            return palisade;
        }
    }
}
