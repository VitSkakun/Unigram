﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls.Views;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.Storage;
using Unigram.Native;
using System.Windows.Input;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsMasksPage : Page
    {
        public SettingsMasksViewModel ViewModel => DataContext as SettingsMasksViewModel;

        public SettingsMasksPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsMasksViewModel>();
        }

        private void ArchivedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsMasksArchivedPage));
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerSetInfo stickerSet)
            {
                await StickerSetView.GetForCurrentView().ShowAsync(stickerSet.Id);
            }
        }

        #region Recycle

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var stickerSet = args.Item as StickerSetInfo;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = stickerSet.Title;
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = Locale.Declension("Stickers", stickerSet.Size);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as Image;

                var cover = stickerSet.Covers.FirstOrDefault();
                if (cover == null || cover.Thumbnail == null)
                {
                    return;
                }

                var file = cover.Thumbnail.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    var buffer = await FileIO.ReadBufferAsync(temp);

                    photo.Source = WebPImage.DecodeFromBuffer(buffer);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    photo.Source = null;
                    ViewModel.ProtoService.Send(new DownloadFile(file.Id, 1));
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        #region Context menu

        private void StickerSet_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var stickerSet = element.Tag as StickerSetInfo;

            if (stickerSet == null || stickerSet.Id == 0)
            {
                return;
            }

            if (stickerSet.IsOfficial)
            {
                CreateFlyoutItem(ref flyout, ViewModel.StickerSetHideCommand, stickerSet, Strings.Resources.StickersHide);
            }
            else
            {
                CreateFlyoutItem(ref flyout, ViewModel.StickerSetHideCommand, stickerSet, Strings.Resources.StickersHide);
                CreateFlyoutItem(ref flyout, ViewModel.StickerSetRemoveCommand, stickerSet, Strings.Resources.StickersRemove);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetShareCommand, stickerSet, Strings.Resources.StickersShare);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetCopyCommand, stickerSet, Strings.Resources.StickersCopy);
            }

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(element, point);
            }
            else if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(element);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, ICommand command, object parameter, string text)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = command != null;
            flyoutItem.Command = command;
            flyoutItem.CommandParameter = parameter;
            flyoutItem.Text = text;

            flyout.Items.Add(flyoutItem);
        }

        #endregion

    }
}
