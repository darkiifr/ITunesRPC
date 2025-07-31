using ItunesRPC.Properties;
using ItunesRPC.Services;
using System.Windows;

namespace ItunesRPC
{
    public partial class UpdateConfigWindow : Window
    {
        private readonly UpdateService _updateService;

        public UpdateConfigWindow(UpdateService updateService)
        {
            InitializeComponent();
            _updateService = updateService;
            
            // Appliquer le thème actuel à cette fenêtre
            ThemeManager.ApplyCurrentThemeToWindow(this);
            
            // Charger les paramètres actuels
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Charger les paramètres depuis les settings
            OwnerTextBox.Text = Settings.Default.GitHubOwner;
            RepoTextBox.Text = Settings.Default.GitHubRepo;
            ReleaseUrlTextBox.Text = Settings.Default.GitHubReleaseUrl;
            CheckUpdateOnStartupCheckBox.IsChecked = Settings.Default.CheckUpdateOnStartup;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Valider les entrées
            if (string.IsNullOrWhiteSpace(OwnerTextBox.Text) || 
                string.IsNullOrWhiteSpace(RepoTextBox.Text) || 
                string.IsNullOrWhiteSpace(ReleaseUrlTextBox.Text))
            {
                MessageBox.Show("Tous les champs sont obligatoires.", "Erreur de validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sauvegarder les paramètres
            _updateService.SaveUpdateSettings(
                OwnerTextBox.Text.Trim(),
                RepoTextBox.Text.Trim(),
                ReleaseUrlTextBox.Text.Trim());

            // Sauvegarder l'option de vérification au démarrage
            Settings.Default.CheckUpdateOnStartup = CheckUpdateOnStartupCheckBox.IsChecked ?? true;
            Settings.Default.Save();

            // Fermer la fenêtre
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Fermer sans sauvegarder
            DialogResult = false;
            Close();
        }
    }
}