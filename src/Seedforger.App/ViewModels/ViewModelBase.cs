using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Seedforger.App.ViewModels {

  public abstract class ViewModelBase : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void Raise(string name) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null) {
      if (Equals(field, value)) return false;
      field = value;
      OnPropertyChanged(name);
      return true;
    }
  }

  /// <summary>Binding proxy for localized strings: <c>{Binding L[card.torrent]}</c>.
  /// Raise PropertyChanged("L") to refresh every localized binding on language change.</summary>
  public sealed class LocProxy {
    public string this[string key] => Seedforger.UI.UiStrings.Get(key);
  }
}
