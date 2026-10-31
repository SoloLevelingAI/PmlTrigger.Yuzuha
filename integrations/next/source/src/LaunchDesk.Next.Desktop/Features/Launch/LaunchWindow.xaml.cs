using System.Windows;
using System.Windows.Controls;
using LaunchDesk.Next.Application.Features.LoginLaunch;
using LaunchDesk.Next.Application.Infrastructure.Contracts;
namespace LaunchDesk.Next.Desktop.Features.Launch;

public partial class LaunchWindow : Window
{
    private readonly LaunchInteraction interaction;
    public LaunchViewModel ViewModel { get; }
    public event EventHandler? OpenDataRequested;
    public LaunchWindow(LaunchInteraction interaction, LaunchViewModel? viewModel = null)
    {
        this.interaction = interaction; ViewModel = viewModel ?? new(); InitializeComponent(); DataContext = ViewModel;
        Loaded += async (_, _) => await Guard(() => interaction.InitializeAsync(ViewModel));
        Closed += (_, _) => interaction.Detach();
    }
    private async Task Guard(Func<Task> action) { try { await action(); } catch (Exception ex) { ViewModel.Status = "操作失败 · " + ex.Message; } }
    private async void OnScan(object sender, RoutedEventArgs e) => await Guard(() => interaction.ScanAsync(ViewModel));
    private async void OnSave(object sender, RoutedEventArgs e) => await Guard(() => interaction.SaveAsync(ViewModel));
    private async void OnLaunch(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy) return;
        var secret = PasswordInput.SecurePassword.Length > 0 ? new PasswordHandle(PasswordInput.SecurePassword) : null; PasswordInput.Clear();
        await Guard(() => interaction.LaunchAsync(ViewModel, secret));
    }
    private async void OnCandidate(object sender, SelectionChangedEventArgs e) { if (e.AddedItems.Count > 0 && e.AddedItems[0] is DiscoveryCandidate c) await Guard(() => interaction.SelectCandidateAsync(ViewModel, c)); }
    private void OnProfile(object sender, SelectionChangedEventArgs e) { if (e.AddedItems.Count > 0 && e.AddedItems[0] is LaunchRequest p) interaction.SelectConfiguration(ViewModel, p); }
    private void OnNew(object sender, RoutedEventArgs e) { ViewModel.Load(new LaunchInput().CreateSnapshot(Guid.Empty)); PasswordInput.Clear(); }
    private void OnHistory(object sender, RoutedEventArgs e) { interaction.RefreshEvidence(ViewModel); Workbench.Visibility = Visibility.Collapsed; HistoryPanel.Visibility = Visibility.Visible; interaction.Detach(); }
    private void OnWorkbench(object sender, RoutedEventArgs e) { Workbench.Visibility = Visibility.Visible; HistoryPanel.Visibility = Visibility.Collapsed; interaction.Reattach(ViewModel); }
    private void OnRefreshRecords(object sender, RoutedEventArgs e) => interaction.RefreshEvidence(ViewModel);
    private void OnOpenData(object sender, RoutedEventArgs e) => OpenDataRequested?.Invoke(this, EventArgs.Empty);
    private sealed class PasswordHandle(System.Security.SecureString value) : ISecretHandle
    {
        public string Reveal() { var p = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(value); try { return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(p); } finally { System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(p); } }
        public void Dispose() => value.Dispose();
    }
}
