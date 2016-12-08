﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using OwncloudUniversal.Services;
using OwncloudUniversal.Shared;
using OwncloudUniversal.Shared.LocalFileSystem;
using OwncloudUniversal.Shared.Model;
using OwncloudUniversal.Views;
using OwncloudUniversal.WebDav;
using Template10.Mvvm;
using Template10.Utils;

namespace OwncloudUniversal.ViewModels
{
    public class FilesPageViewModel : ViewModelBase
    {
        private DavItem _selectedItem;
        private readonly WebDavItemService _davItemService;
        private readonly SyncedFoldersService _syncedFolderService;
        private ObservableCollection<DavItem> _itemsList;

        public FilesPageViewModel()
        {
            _davItemService = new WebDavItemService();
            _syncedFolderService = new SyncedFoldersService();
            UploadItemCommand = new DelegateCommand(async () => await UploadItem() );   
            RefreshCommand = new DelegateCommand(async () => await LoadItems());
            AddToSyncCommand = new DelegateCommand<object>(async parameter => await RegisterFolderForSync(parameter));
        }

        public ICommand UploadItemCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand AddToSyncCommand { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);
            var item = parameter as DavItem;
            if (item == null)
            {
                item = new DavItem();
                item.EntityId = Configuration.ServerUrl;
                item.IsCollection = true;
            }
            if (item.IsCollection)
            {
                _selectedItem = item;
                await LoadItems();
            }
        }
        
        public ObservableCollection<DavItem> ItemsList
        {
            get { return _itemsList; }
            private set
            {
                _itemsList = value;
                RaisePropertyChanged();
            }
        }

        public DavItem SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                if(SelectedItem.IsCollection)
                    NavigationService.Navigate(typeof(FilesPage), value, new SuppressNavigationTransitionInfo());
            } 
        }
        
        private async Task LoadItems()
        {
            InidcatorService.GetDefault().ShowBar();
            var items = await _davItemService.GetItemsAsync(new Uri(SelectedItem.EntityId, UriKind.RelativeOrAbsolute));
            items.RemoveAt(0);
            ItemsList = items.OrderBy(x => !x.IsCollection).Cast<DavItem>().ToObservableCollection();
            InidcatorService.GetDefault().HideBar();
        }

        private async Task UploadItem()
        {
            //TODO move this functionality to new page
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            var files = await picker.PickMultipleFilesAsync();
            if (files.Count == 0) return;
            var itemsToUpload = await _BuildRemoteItemsForUploadAsync(files.ToList());
            foreach (var item in itemsToUpload)
            {
                await _davItemService.UploadItemAsync(item, _selectedItem.Href);
            }
            MessageDialog dia = new MessageDialog("Upload finished.");
            await dia.ShowAsync();
            await LoadItems();
        }

        private async Task<List<AbstractItem>> _BuildRemoteItemsForUploadAsync(List<StorageFile> files)
        {
            List<AbstractItem> result = new List<AbstractItem>();
            foreach (var storageFile in files)
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("uploadFile", storageFile);
                var props = await storageFile.GetBasicPropertiesAsync();

                LocalItem item = new LocalItem(null, storageFile, props);
                result.Add(item);
            }
            return result;
        }

        private async Task RegisterFolderForSync(object parameter)
        {
            if (parameter is DavItem)
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add(".");
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null)
                    return;
                await _syncedFolderService.AddFolderToSyncAsync(folder, (DavItem) parameter);
                await LoadItems();
            }
            else
            {
                //?
            }
        }


    }
}
