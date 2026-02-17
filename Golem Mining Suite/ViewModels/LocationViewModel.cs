using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class LocationViewModel : ObservableObject
    {
        private readonly IMiningDataService _miningDataService;

        private List<LocationData> _allLocations = new();
        private string _targetName = "";

        [ObservableProperty]
        private string _title = "";

        [ObservableProperty]
        private bool _isAsteroidMode;

        [ObservableProperty]
        private bool _isROCMode;
        
        [ObservableProperty]
        private bool _isMineral;

        [ObservableProperty]
        private ObservableCollection<LocationData> _locations = new();

        // Filter Lists
        [ObservableProperty]
        private ObservableCollection<string> _planets = new();

        [ObservableProperty]
        private ObservableCollection<string> _oreTypes = new();

        // Selected Filters
        [ObservableProperty]
        private string _selectedSystem = "All"; // All, Stanton, Pyro

        [ObservableProperty]
        private string _selectedLocationType = "All"; // All, MiningBase, Lagrange, Belt

        [ObservableProperty]
        private string? _selectedPlanet;

        [ObservableProperty]
        private string? _selectedOreType;

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private string _minDepositPercentText = "";

        [ObservableProperty]
        private string _minMineralPercentText = "";
        
        // Cluster Rock Popup
        [ObservableProperty]
        private List<ClusterRockInfo> _clusterRocks = new();
        
        [ObservableProperty]
        private string _popupTitle = "";
        
        [ObservableProperty]
        private bool _isPopupOpen;

        public LocationViewModel(IMiningDataService miningDataService)
        {
            _miningDataService = miningDataService;
            Locations = new ObservableCollection<LocationData>();
            Planets = new ObservableCollection<string>();
            OreTypes = new ObservableCollection<string>();
        }

        public void Initialize(string name, bool isMineral, bool isAsteroid, bool isRoc)
        {
            _targetName = name;
            IsMineral = isMineral;
            IsAsteroidMode = isAsteroid;
            IsROCMode = isRoc;

            // Set Title
            if (IsAsteroidMode)
            {
                Title = IsMineral ? $"Best Locations for {name}" : $"Best Ore Types for {name}";
            }
            else
            {
                Title = IsMineral ? $"Best Locations for {name}" : $"Best Locations for {name}";
            }

            // Load Data
            LoadData();

            // Initialize Filters
            if (IsAsteroidMode) LoadOreTypeFilter();
            if(!IsAsteroidMode) LoadPlanetFilter();

            // Default selections
            if (Planets.Count > 0) SelectedPlanet = Planets[0];
            if (OreTypes.Count > 0) SelectedOreType = OreTypes[0];
        }

        private void LoadData()
        {
            if (IsROCMode)
            {
                LoadROCLocations(_targetName);
            }
            else if (IsAsteroidMode)
            {
                if (IsMineral)
                {
                    LoadAsteroidMineralLocations(_targetName);
                }
                else
                {
                    LoadAsteroidOreTypeLocations(_targetName);
                }
            }
            else
            {
                if (IsMineral)
                {
                    LoadMineralDeposits(_targetName);
                }
                else
                {
                    LoadLocations(_targetName);
                }
            }
        }

        private void LoadMineralDeposits(string mineralName)
        {
            var mineralDepositData = _miningDataService.GetSurfaceMineralToDepositMapping();
            var depositLocationData = _miningDataService.GetAllSurfaceDepositLocations();

            if (!mineralDepositData.ContainsKey(mineralName))
            {
                _allLocations = new List<LocationData>();
                return;
            }

            var depositsWithMineral = mineralDepositData[mineralName];
            var planetMineralData = new List<LocationData>();

            foreach (var deposit in depositsWithMineral)
            {
                string depositName = deposit.LocationName;
                string mineralPercentage = deposit.Chance;

                if (depositLocationData.ContainsKey(depositName))
                {
                    var planetsWithDeposit = depositLocationData[depositName];

                    foreach (var planet in planetsWithDeposit)
                    {
                        double depositSpawnRate = 0;
                        double.TryParse(planet.Chance.Replace("%", ""), out depositSpawnRate);

                        planetMineralData.Add(new LocationData
                        {
                            LocationName = $"{planet.LocationName} - {depositName}",
                            DepositChance = planet.Chance,
                            MineralChance = mineralPercentage,
                            System = planet.System,
                            SortValue = depositSpawnRate,
                            Signature = _miningDataService.GetSurfaceDepositSignature(depositName, planet.System),
                            DepositType = depositName
                        });
                    }
                }
            }

            _allLocations = planetMineralData;
            ApplyFilter();
        }

        private void LoadAsteroidMineralLocations(string mineralName)
        {
            var mineralOreTypeData = _miningDataService.GetAsteroidMineralToOreTypeMapping();
            var oreTypeLocationData = _miningDataService.GetAsteroidOreTypeLocations();

            if (!mineralOreTypeData.ContainsKey(mineralName))
            {
                _allLocations = new List<LocationData>();
                return;
            }

            var oreTypesWithMineral = mineralOreTypeData[mineralName];
            var asteroidMineralData = new List<LocationData>();

            foreach (var oreType in oreTypesWithMineral)
            {
                string oreTypeName = oreType.LocationName;
                string mineralPercentage = oreType.Chance;

                if (oreTypeLocationData.ContainsKey(oreTypeName))
                {
                    var locationsWithOreType = oreTypeLocationData[oreTypeName];

                    foreach (var location in locationsWithOreType)
                    {
                        double oreTypeSpawnRate = 0;
                        double.TryParse(location.Chance.Replace("%", ""), out oreTypeSpawnRate);

                        asteroidMineralData.Add(new LocationData
                        {
                            LocationName = $"{location.LocationName} - {oreTypeName}",
                            DepositChance = location.Chance,
                            MineralChance = mineralPercentage,
                            System = location.System,
                            SortValue = oreTypeSpawnRate,
                            Signature = _miningDataService.GetAsteroidOreTypeSignature(oreTypeName),
                            DepositType = oreTypeName
                        });
                    }
                }
            }

            _allLocations = asteroidMineralData;
            ApplyFilter();
        }

        private void LoadLocations(string depositName)
        {
            var depositLocations = _miningDataService.GetAllSurfaceDepositLocations();

            if (depositLocations.ContainsKey(depositName))
            {
                _allLocations = depositLocations[depositName].Select(loc => {
                     double sortVal = 0;
                     double.TryParse(loc.Chance.Replace("%", ""), out sortVal);
                     
                     return new LocationData
                     {
                        LocationName = loc.LocationName,
                        Chance = loc.Chance,
                        DepositChance = loc.Chance,
                        MineralChance = "-",
                        System = loc.System,
                        SortValue = sortVal,
                        Signature = _miningDataService.GetSurfaceDepositSignature(depositName, loc.System),
                        DepositType = depositName
                     };
                }).ToList();
                ApplyFilter();
            }
            else
            {
                 _allLocations = new List<LocationData>();
                 ApplyFilter();
            }
        }

        private void LoadAsteroidOreTypeLocations(string oreTypeName)
        {
            var oreTypeLocations = _miningDataService.GetAsteroidOreTypeLocations();

            if (oreTypeLocations.ContainsKey(oreTypeName))
            {
                _allLocations = oreTypeLocations[oreTypeName].Select(loc => {
                     double sortVal = 0;
                     double.TryParse(loc.Chance.Replace("%", ""), out sortVal);

                     return new LocationData
                     {
                        LocationName = loc.LocationName,
                        Chance = loc.Chance,
                        DepositChance = loc.Chance,
                        MineralChance = "-",
                        System = loc.System,
                        SortValue = sortVal,
                        Signature = _miningDataService.GetAsteroidOreTypeSignature(oreTypeName),
                        DepositType = oreTypeName
                     };
                }).ToList();
                ApplyFilter();
            }
             else
            {
                 _allLocations = new List<LocationData>();
                 ApplyFilter();
            }
        }

        private void LoadROCLocations(string rockType)
        {
            var rocData = _miningDataService.GetROCLocationMapping();

            if (rocData.ContainsKey(rockType))
            {
                _allLocations = rocData[rockType];
                // Ensure SortValue is set if not already (it seems to be set in data, but good to be safe)
                ApplyFilter();
            }
            else
            {
                _allLocations = new List<LocationData>();
                ApplyFilter();
            }
        }

        private void LoadPlanetFilter()
        {
            if (_allLocations == null) return;
            Planets.Clear();
            Planets.Add("All");

            var planetList = _allLocations
                .Select(l => l.LocationName.Split('-')[0].Trim())
                .Distinct()
                .OrderBy(p => p)
                .ToList();
            
            foreach(var p in planetList) Planets.Add(p);
        }

        private void LoadOreTypeFilter()
        {
            OreTypes.Clear();
            OreTypes.Add("All Ore Types");
            foreach(var type in new[] { "C-Type", "E-Type", "I-Type", "M-Type", "P-Type", "Q-Type", "S-Type" })
            {
                OreTypes.Add(type);
            }
        }

        // OnProperty methods to trigger filters
        partial void OnSelectedSystemChanged(string value) => ApplyFilter();
        partial void OnSelectedLocationTypeChanged(string value) => ApplyFilter();
        partial void OnSelectedPlanetChanged(string? value) => ApplyFilter();
        partial void OnSelectedOreTypeChanged(string? value) => ApplyFilter();
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnMinDepositPercentTextChanged(string value) => ApplyFilter();
        partial void OnMinMineralPercentTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_allLocations == null) return;

            IEnumerable<LocationData> filtered = _allLocations;

            // 1. System
            if (SelectedSystem != "All")
            {
                filtered = filtered.Where(l => l.System == SelectedSystem);
            }

            // 2. Location Type (Asteroid)
            if (IsAsteroidMode && SelectedLocationType != "All")
            {
                filtered = filtered.Where(l =>
                {
                    string name = l.LocationName.ToLower();
                    switch (SelectedLocationType)
                    {
                        case "MiningBase": return name.Contains("mining base");
                        case "Lagrange": return Regex.IsMatch(name, @"\s+l[1-5](\s|$)");
                        case "Belt": return name.Contains("belt");
                        default: return true;
                    }
                });
            }

            // 3. Ore Type
            if (IsAsteroidMode && SelectedOreType != "All Ore Types" && !string.IsNullOrEmpty(SelectedOreType))
            {
                filtered = filtered.Where(l => l.DepositChance != null && l.LocationName.Contains(SelectedOreType));
            }

            // 4. Planet
            if (!IsAsteroidMode && SelectedPlanet != "All" && !string.IsNullOrEmpty(SelectedPlanet))
            {
                 filtered = filtered.Where(l => l.LocationName.ToLower().StartsWith(SelectedPlanet.ToLower()));
            }

            // 5. Search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(l => l.LocationName.ToLower().Contains(SearchText.ToLower()));
            }

            // 6. Min Deposit
            if (double.TryParse(MinDepositPercentText, out double minDep) && minDep > 0)
            {
                filtered = filtered.Where(l =>
                {
                    string val = l.DepositChance?.Replace("%", "") ?? "0";
                    return double.TryParse(val, out double d) && d >= minDep;
                });
            }

            // 7. Min Mineral
            if (IsMineral && double.TryParse(MinMineralPercentText, out double minMin) && minMin > 0)
            {
                filtered = filtered.Where(l =>
                {
                    string val = l.MineralChance?.Replace("%", "") ?? "0";
                    return double.TryParse(val, out double m) && m >= minMin;
                });
            }

            // Sort
            var sorted = filtered.OrderByDescending(l => l.SortValue).ToList();

            Locations.Clear();
            foreach(var item in sorted) Locations.Add(item);
        }
        
        [RelayCommand] // For Systems
        private void SetSystemFilter(string system)
        {
            SelectedSystem = system;
        }

        [RelayCommand] // For Location Types
        private void SetLocationTypeFilter(string type)
        {
            SelectedLocationType = type;
        }

        [RelayCommand] // Show Signature
        private void ShowSignature(LocationData locationData)
        {
             if (locationData == null || string.IsNullOrEmpty(locationData.DepositType)) return;
             
             PopupTitle = locationData.DepositType;
             
             if(IsAsteroidMode)
                ClusterRocks = _miningDataService.GetAsteroidClusterRockData(locationData.DepositType);
             else
                ClusterRocks = _miningDataService.GetSurfaceClusterRockData(locationData.DepositType);
             
             IsPopupOpen = true;
        }

        [RelayCommand]
        private void CloseSignature()
        {
            IsPopupOpen = false;
        }
    }
}
