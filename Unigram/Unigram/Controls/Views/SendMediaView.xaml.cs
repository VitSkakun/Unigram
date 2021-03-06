﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Entities;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class SendMediaView : ContentDialogBase, INotifyPropertyChanged
    {
        public DialogViewModel ViewModel { get; set; }

        public MvxObservableCollection<StorageMedia> Items { get; } = new MvxObservableCollection<StorageMedia>();
        public MvxObservableCollection<StorageMedia> SelectedItems { get; } = new MvxObservableCollection<StorageMedia>();

        private StorageMedia _selectedItem;
        public StorageMedia SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                }

                if (_selectedItem != null)
                {
                    CaptionInput.Text = _selectedItem.Caption ?? string.Empty;
                }
            }
        }

        public int SelectedIndex
        {
            get
            {
                var item = SelectedItem;
                if (item == null)
                {
                    return 0;
                }

                return SelectedItems.IndexOf(item) + 1;
            }
        }

        private bool _isTtlEnabled;
        public bool IsTTLEnabled
        {
            get
            {
                return _isTtlEnabled;
            }
            set
            {
                if (_isTtlEnabled != value)
                {
                    _isTtlEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTTLEnabled"));
                }
            }
        }

        public bool IsGroupingEnabled
        {
            get
            {
                return SelectedItems.Count > 1 && !SelectedItems.Any(x => x.Ttl.HasValue);
            }
        }

        private bool _isGrouped;
        public bool IsGrouped
        {
            get
            {
                return _isGrouped;
            }
            set
            {
                if (_isGrouped != value)
                {
                    _isGrouped = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGrouped"));
                }
            }
        }

        private ICollection _autocomplete;
        public ICollection Autocomplete
        {
            get
            {
                return _autocomplete;
            }
            set
            {
                if (_autocomplete != value)
                {
                    _autocomplete = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Autocomplete"));
                }
            }
        }

        #region Editing

        public bool IsEditing
        {
            get
            {
                return _isEditingCompression || _isEditingCropping;
            }
        }

        private bool _isEditingCompression;
        public bool IsEditingCompression
        {
            get
            {
                return _isEditingCompression;
            }
            set
            {
                if (_isEditingCompression != value)
                {
                    _isEditingCompression = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditingCompression"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditing"));
                }
            }
        }

        private bool _isEditingCropping;
        public bool IsEditingCropping
        {
            get
            {
                return _isEditingCropping;
            }
            set
            {
                if (_isEditingCropping != value)
                {
                    _isEditingCropping = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditingCropping"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditing"));
                }
            }
        }

        #endregion

        #region Binding

        private string ConvertAccept(bool editing)
        {
            return editing ? "\uE10B" : "\uE725";
        }

        private string ConvertCompression(StorageMedia media, double compression)
        {
            var value = (int)compression;
            if (media is StorageVideo video)
            {
                return video.ToString(value);
            }

            return null;
        }

        private string ConvertGrouped(bool grouped)
        {
            return grouped ? Strings.Resources.GroupPhotosHelp : Strings.Resources.SinglePhotosHelp;
        }

        private bool ConvertSelected(StorageMedia media)
        {
            return SelectedItems.Contains(media);
        }

        #endregion

        public SendMediaView()
        {
            InitializeComponent();
            DataContext = this;

            //var seconds = new int[29];
            //for (int i = 0; i < seconds.Length; i++)
            //{
            //    seconds[i] = i;
            //}

            //TTLSeconds.ItemsSource = seconds;

            ProportionsBox.SelectionChanged += (s, args) =>
            {
                Cropper.Proportions = (ImageCroppingProportions)ProportionsBox.SelectedItem;
            };

            TTLSeconds.RegisterPropertyChangedCallback(GlyphButton.GlyphProperty, OnSecondsChanged);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            if (IsEditingCompression)
            {
                e.Handled = true;
                IsEditingCompression = false;
            }
            else if (IsEditingCropping)
            {
                e.Handled = true;
                IsEditingCropping = false;
            }
            else
            {
                e.Handled = true;
                Hide(ContentDialogBaseResult.None);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            IsGrouped = SettingsService.Current.IsSendGrouped;

            if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                CaptionInput.Focus(FocusState.Keyboard);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            KeyboardPlaceholder.Height = new GridLength(args.OccludedRect.Height);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        public void Accept()
        {
            Accept_Click(null, null);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Items == null)
            {
                Hide(ContentDialogBaseResult.Cancel);
                return;
            }

            if (IsEditingCompression && SelectedItem is StorageVideo video)
            {
                video.Compression = (int)CompressionValue.Value;

                IsEditingCompression = false;
                return;
            }

            if (IsEditingCropping && SelectedItem is StorageMedia media)
            {
                media.CropRectangle = Cropper.CropRectangle;
                media.Refresh();

                IsEditingCropping = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                return;
            }

            if (SelectedItem != null && SelectedItems.IsEmpty())
            {
                SelectedItems.Add(SelectedItem);
            }

            if (IsGroupingEnabled)
            {
                SettingsService.Current.IsSendGrouped = IsGrouped;
            }

            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (IsEditingCompression && SelectedItem is StorageVideo video)
            {
                IsEditingCompression = false;
                return;
            }

            if (IsEditingCropping && SelectedItem is StorageMedia media)
            {
                IsEditingCropping = false;
                return;
            }

            Hide(ContentDialogBaseResult.Cancel);
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    Accept_Click(null, null);
                }

                Flip.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private async void More_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.MediaTypes);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var storage = await StorageMedia.CreateAsync(file, true);
                    if (storage != null)
                    {
                        Items.Add(storage);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void TTLSeconds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //VisualStateManager.GoToState(TTLSeconds, TTLSeconds.SelectedIndex == 0 ? "Unselected" : "Selected", false);
        }

        private void OnSecondsChanged(DependencyObject sender, DependencyProperty dp)
        {
            VisualStateManager.GoToState(TTLSeconds, SelectedItem.Ttl == null ? "Unselected" : "Selected", false);
            //VisualStateManager.GoToState(this, SelectedItem.TTLSeconds == null ? "Unselected" : "Selected", false);

            // TODO: WRONG!!!
            if (SelectedItem.Ttl == null)
            {
                TTLSeconds.ClearValue(Button.ForegroundProperty);
            }
            else
            {
                TTLSeconds.Foreground = LayoutRoot.Resources["SystemControlForegroundAccentBrush"] as SolidColorBrush;
            }
        }

        private async void TTLSeconds_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageTtlView(SelectedItem.IsPhoto);
            dialog.Value = SelectedItem.Ttl != null && SelectedItem.Ttl > 0 ? SelectedItem.Ttl : ViewModel.Settings.LastMessageTtl;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                if (dialog.Value != null && dialog.Value > 0)
                {
                    ViewModel.Settings.LastMessageTtl = dialog.Value ?? 7;
                }

                SelectedItem.Ttl = dialog.Value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
            }
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var text = CaptionInput.Text.ToString();

            if (e.ClickedItem is User user && BubbleTextBox.SearchByUsername(text.Substring(0, Math.Min(CaptionInput.SelectionStart, text.Length)), out string username, out int index))
            {
                var insert = $"@{user.Username} ";
                var start = CaptionInput.SelectionStart - 1 - username.Length;
                var part1 = text.Substring(0, start);
                var part2 = text.Substring(start + 1 + username.Length);

                CaptionInput.Text = part1 + insert + part2;
                CaptionInput.SelectionStart = start + insert.Length;

                Autocomplete = null;
            }
            else if (e.ClickedItem is EmojiSuggestion emoji && BubbleTextBox.SearchByEmoji(text.Substring(0, Math.Min(CaptionInput.SelectionStart, text.Length)), out string replacement))
            {
                var insert = $"{emoji.Emoji} ";
                var start = CaptionInput.SelectionStart - 1 - replacement.Length;
                var part1 = text.Substring(0, start);
                var part2 = text.Substring(start + 1 + replacement.Length);

                CaptionInput.Text = part1 + insert + part2;
                CaptionInput.SelectionStart = start + insert.Length;

                Autocomplete = null;
            }
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var height = e.NewSize.Height;
            var padding = ListAutocomplete.ActualHeight - Math.Min(154, ListAutocomplete.Items.Count * 44);

            //ListAutocomplete.Padding = new Thickness(0, padding, 0, 0);
            AutocompleteHeader.Margin = new Thickness(0, padding, 0, -height);
            AutocompleteHeader.Height = height;

            Debug.WriteLine("Autocomplete size changed");
        }

        private void Compress_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageVideo video)
            {
                IsEditingCompression = true;
                CompressionValue.Maximum = video.MaxCompression - 1;
                CompressionValue.Value = video.Compression;
            }
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                IsEditingCropping = true;

                if (media.Bitmap is SoftwareBitmapSource source)
                {
                    var width = media.Width;
                    var height = media.Height;

                    if (width > 1280 || height > 1280)
                    {
                        double ratioX = (double)1280 / width;
                        double ratioY = (double)1280 / height;
                        double ratio = Math.Min(ratioX, ratioY);

                        width = (uint)(width * ratio);
                        height = (uint)(height * ratio);
                    }

                    //var container = Flip.ContainerFromItem(Flip.SelectedItem) as SelectorItem;
                    //if (container != null)
                    //{
                    //    var content = container.ContentTemplateRoot as Border;
                    //    var zoom = content.Child as Viewbox;

                    //    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("Crop", zoom);
                    //}

                    Cropper.SetSource(media.File, source, width, height);
                    Cropper.Proportions = media.CropProportions;
                    Cropper.CropRectangle = media.CropRectangle ?? Rect.Empty;

                    ProportionsBox.ItemsSource = ImageCropper.GetProportionsFor(width, height);
                    ProportionsBox.SelectedItem = media.CropProportions;
                }
            }
        }

        private void Proportions_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                if (media.CropProportions == ImageCroppingProportions.Custom)
                {
                    var width = media.Width;
                    var height = media.Height;

                    if (width > 1280 || height > 1280)
                    {
                        double ratioX = (double)1280 / width;
                        double ratioY = (double)1280 / height;
                        double ratio = Math.Min(ratioX, ratioY);

                        width = (uint)(width * ratio);
                        height = (uint)(height * ratio);
                    }

                    var flyout = new MenuFlyout();
                    var items = ImageCropper.GetProportionsFor(width, height);

                    var handler = new RoutedEventHandler((s, args) =>
                    {
                        if (s is MenuFlyoutItem option)
                        {
                            media.CropProportions = (ImageCroppingProportions)option.Tag;
                            Cropper.Proportions = media.CropProportions;
                        }
                    });

                    foreach (var item in items)
                    {
                        var option = new MenuFlyoutItem();
                        option.Click += handler;
                        option.Text = ProportionsToLabelConverter.Convert(item);
                        option.Tag = item;
                        option.MinWidth = 140;
                        option.HorizontalContentAlignment = HorizontalAlignment.Center;

                        flyout.Items.Add(option);
                    }

                    if (flyout.Items.Count > 0)
                    {
                        flyout.ShowAt((Button)sender);
                    }
                }
                else
                {
                    media.CropProportions = ImageCroppingProportions.Custom;
                    Cropper.Proportions = ImageCroppingProportions.Custom;
                }
            }
        }

        private void ResetCrop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                media.CropProportions = ImageCroppingProportions.Custom;
                Cropper.Reset(ImageCroppingProportions.Custom);
                Cropper.Proportions = ImageCroppingProportions.Custom;
            }
        }

        private Visibility ConvertProportions(ImageCroppingProportions proportions, bool positive)
        {
            if (positive)
            {
                return proportions == ImageCroppingProportions.Custom ? Visibility.Collapsed : Visibility.Visible;
            }

            return proportions == ImageCroppingProportions.Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetItems(ObservableCollection<StorageMedia> storages)
        {
            Items.ReplaceWith(storages);
            SelectedItems.ReplaceWith(storages.Where(x => x.IsSelected));
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            var item = SelectedItem;
            if (item == null)
            {
                return;
            }

            if (SelectedItems.Contains(item))
            {
                SelectedItems.Remove(item);
            }
            else
            {
                SelectedItems.Add(item);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
        }

        private void Select_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Select_Click(null, null);
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            var package = Clipboard.GetContent();
            if (package.Contains(StandardDataFormats.Bitmap))
            {
                e.Handled = true;

                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();
                var cache = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\paste.jpg", CreationCollisionOption.ReplaceExisting);

                using (var stream = await bitmap.OpenReadAsync())
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    var buffer = new byte[(int)stream.Size];
                    reader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(cache, buffer);

                    var photo = await StoragePhoto.CreateAsync(cache, true) as StorageMedia;
                    if (photo == null)
                    {
                        photo = await StorageVideo.CreateAsync(cache, true);
                    }

                    if (photo == null)
                    {
                        return;
                    }

                    media.Add(photo);
                }

                if (package.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = await package.GetTextAsync();
                }

                foreach (var item in media)
                {
                    Items.Add(item);
                }

                SelectedItem = media[0];
            }
            else if (package.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;

                var items = await package.GetStorageItemsAsync();
                var media = new ObservableCollection<StorageMedia>();
                var files = new List<StorageFile>(items.Count);

                foreach (StorageFile file in items)
                {
                    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        media.Add(await StoragePhoto.CreateAsync(file, true));
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        media.Add(await StorageVideo.CreateAsync(file, true));
                    }

                    files.Add(file);
                }

                // Send compressed __only__ if user is dropping photos and videos only
                if (media.Count > 0 && media.Count == files.Count)
                {
                    foreach (var item in media)
                    {
                        Items.Add(item);
                    }

                    SelectedItem = media[0];
                }
                else if (files.Count > 0)
                {
                    // Not supported here!
                }
            }
        }
    }

    public class HeaderFlipView : FlipView
    {
        #region Content

        public object Content
        {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(HeaderFlipView), new PropertyMetadata(null));

        #endregion
    }
}
