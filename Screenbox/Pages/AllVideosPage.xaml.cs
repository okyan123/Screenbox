﻿using Screenbox.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Screenbox.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AllVideosPage : Page
    {
        internal AllVideosPageViewModel ViewModel => (AllVideosPageViewModel)DataContext;

        internal CommonViewModel Common { get; }

        public AllVideosPage()
        {
            this.InitializeComponent();
            DataContext = App.Services.GetRequiredService<AllVideosPageViewModel>();
            Common = App.Services.GetRequiredService<CommonViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.FetchVideosAsync();
        }

        private async void VideosGridView_OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Phase != 0) return;
            if (args.Item is MediaViewModel { Thumbnail: null } media)
            {
                await media.LoadThumbnailAsync();
            }
        }
    }
}