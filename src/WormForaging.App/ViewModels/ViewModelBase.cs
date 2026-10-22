using Prism.Mvvm;

namespace WormForaging.App.ViewModels;

/// <summary>
/// Base class for the application's view models.
/// </summary>
/// <remarks>
/// Derives from Prism's <see cref="BindableBase"/> for change notification
/// (<c>SetProperty</c> / <c>RaisePropertyChanged</c>), matching the MVVM foundation of the larger
/// HyperSpace framework so view models can move between the two codebases. Prism.Core carries no
/// IoC container, so adopting it does not conflict with the Microsoft DI container used here.
/// </remarks>
public abstract class ViewModelBase : BindableBase
{
}
