﻿using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Uwp.UI;
using Screenbox.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Screenbox.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SongsPage : Page
    {
        internal SongsPageViewModel ViewModel => (SongsPageViewModel)DataContext;

        internal CommonViewModel Common { get; }

        private bool _navigatedBack;

        public SongsPage()
        {
            this.InitializeComponent();
            DataContext = Ioc.Default.GetRequiredService<SongsPageViewModel>();
            Common = Ioc.Default.GetRequiredService<CommonViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.FetchSongs();
            _navigatedBack = e.NavigationMode == NavigationMode.Back;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.OnNavigatedFrom();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (SongListView.FindDescendant<ScrollViewer>() is { } scrollViewer)
                Common.ScrollingStates[nameof(SongsPage) + Frame.BackStackDepth] = scrollViewer.VerticalOffset;
        }

        private void SongListView_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_navigatedBack && Common.ScrollingStates.TryGetValue(nameof(SongsPage) + Frame.BackStackDepth, out double verticalOffset))
            {
                SongListView.FindDescendant<ScrollViewer>()?.ChangeView(null, verticalOffset, null, true);
            }
        }
    }
}
