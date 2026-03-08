using Palisades.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Palisades.View
{
    public partial class AiSettings : Window
    {
        private readonly Helpers.AiSettings settings;

        public AiSettings()
        {
            InitializeComponent();

            settings = Helpers.AiSettings.Load();

            List<string> providers = settings.Providers.Keys.ToList();
            providers.Sort();

            foreach (string provider in providers)
            {
                ProviderCombo.Items.Add(provider);
            }

            if (ProviderCombo.Items.Count > 0)
            {
                string current = settings.Provider;
                if (!providers.Contains(current))
                {
                    current = providers[0];
                }
                ProviderCombo.SelectedItem = current;
            }
        }

        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProviderCombo.SelectedItem == null)
            {
                return;
            }

            string providerName = ProviderCombo.SelectedItem.ToString() ?? string.Empty;
            if (!settings.Providers.TryGetValue(providerName, out ProviderSettings? provider))
            {
                return;
            }

            BaseUrlText.Text = provider.BaseUrl;
            EnvKeyText.Text = provider.EnvKey;
            ApiKeyPassword.Password = provider.ApiKey;
            BindRecommendedModels(provider);
        }

        private void BindRecommendedModels(ProviderSettings provider)
        {
            ModelCombo.ItemsSource = null;
            List<string> items = provider.RecommendedModels?
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(provider.Model) && !items.Contains(provider.Model, System.StringComparer.OrdinalIgnoreCase))
            {
                items.Insert(0, provider.Model);
            }

            ModelCombo.ItemsSource = items;
            ModelCombo.Text = provider.Model;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ProviderCombo.SelectedItem == null)
            {
                return;
            }

            string providerName = ProviderCombo.SelectedItem.ToString() ?? string.Empty;
            if (!settings.Providers.TryGetValue(providerName, out ProviderSettings? provider))
            {
                provider = new ProviderSettings();
                settings.Providers[providerName] = provider;
            }

            provider.BaseUrl = BaseUrlText.Text.Trim();
            provider.Model = (ModelCombo.Text ?? string.Empty).Trim();
            provider.EnvKey = EnvKeyText.Text.Trim();
            provider.ApiKey = ApiKeyPassword.Password.Trim();
            provider.RecommendedModels ??= new List<string>();
            if (!string.IsNullOrWhiteSpace(provider.Model) && !provider.RecommendedModels.Contains(provider.Model, System.StringComparer.OrdinalIgnoreCase))
            {
                provider.RecommendedModels.Insert(0, provider.Model);
            }
            settings.Provider = providerName;

            settings.Save();
            MessageBox.Show("AI 配置已保存。", "Palisades", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
