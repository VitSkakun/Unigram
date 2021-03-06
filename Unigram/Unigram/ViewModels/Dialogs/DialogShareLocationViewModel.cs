﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Utils;
using Unigram.Core.Common;
using Unigram.Core.Services;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogShareLocationViewModel : TLViewModelBase
    {
        private readonly ILocationService _locationService;

        public DialogShareLocationViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService foursquareService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _locationService = foursquareService;

            Items = new MvxObservableCollection<Venue>();
            OnNavigatedToAsync(null, NavigationMode.New, null);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var location = await _locationService.GetPositionAsync();
            if (location == null)
            {
                Location = null;
                return;
            }

            Location = location;

            var venues = await _locationService.GetVenuesAsync(0, location.Point.Position.Latitude, location.Point.Position.Longitude);
            Items.ReplaceWith(venues);
        }

        public MvxObservableCollection<Venue> Items { get; private set; }

        private Geocoordinate _location;
        public Geocoordinate Location
        {
            get
            {
                return _location;
            }
            set
            {
                Set(ref _location, value);
            }
        }

        #region Search

        public async void Find(string query)
        {
            var location = _location;
            if (location == null)
            {
                return;
            }

            var venues = await _locationService.GetVenuesAsync(0, location.Point.Position.Latitude, location.Point.Position.Longitude, query);
            Search = new MvxObservableCollection<Venue>(venues);
        }

        private MvxObservableCollection<Venue> _search;
        public MvxObservableCollection<Venue> Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }

        #endregion
    }
}
