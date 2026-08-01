using System;
using System.Collections.Generic;
using System.Linq;
using Economy;
using Economy.ItemType;
using Economy.Products;
using GameDatabase.Enums;
using UnityEngine;
using UnityEngine.UI;
using GameModel.Quests;
using GameServices.Player;
using Galaxy;
using Services.Audio;
using Services.Gui;
using Services.Localization;
using Services.Messenger;
using Services.Resources;
using Session;
using Session.Content;
using Zenject;
using CommonComponents;
using GameServices.Gui;

namespace ViewModel
{
    namespace Dialogs
    {
        public class SpecialMarketDialog : MonoBehaviour
        {
            [Inject] private readonly IMessenger _messenger;
            [Inject] private readonly ILocalization _localization;
            [Inject] private readonly ISoundPlayer _soundPlayer;
            [Inject] private readonly PlayerResources _playerResources;
            [Inject] private readonly IResourceLocator _resourceLocator;
            [Inject] private readonly GuiHelper _helper;
            [Inject] private readonly Galaxy.StarMap _starMap;
            [Inject] private readonly StarData _starData;
            [Inject] private readonly InventoryFactory _inventoryFactory;
            [Inject] private readonly ISessionData _session;

            public enum Filter
            {
                All = -1,
                Resource = 0,
                Ship = 1,
                Weapon = 2,
                Module = 3,
                Other = 4,
            }

            [SerializeField] private ToggleGroup ItemsGroup;
            [SerializeField] private Toggle BuyToggle;
            [SerializeField] private Toggle Buy_Toggle;
            [SerializeField] private Toggle SellToggle;
            [SerializeField] private AudioClip Sound;
            [SerializeField] private Button BuyButton;
            [SerializeField] private Button SellButton;
            [SerializeField] private Button _sellJunkButton;
            [SerializeField] private GameObject QuantityPanel;
            [SerializeField] private Slider QuantitySlider;
            [SerializeField] private RadioGroupViewModel FilterGroup;
            [SerializeField] private Common.PricePanel PricePanel;
            [SerializeField] private GameObject DescriptionPanel;
            [SerializeField] private Text QuantityText;
            [SerializeField] private Text NameText;
            [SerializeField] private Text DescrtiptionText;
            [SerializeField] private Image Icon;
            [SerializeField] private GameObject EmptyLabel;
            [SerializeField] private Text MoneyText;
            [SerializeField] private Text StarText;
            [SerializeField] private GameObject StarPanel;

            [SerializeField] MarketContentFiller ContentFiller;
            [SerializeField] ListScrollRect ItemList;

            private enum Tab { BlackMarketAndStations, ArenaAndXmas, Sell }

            public void Initialize(WindowArgs args)
            {
                _messenger.AddListener(EventType.IapItemsRefreshed, OnIapItemsChanged);
                _messenger.AddListener<Money>(EventType.MoneyValueChanged, value => UpdateStats());
                _messenger.AddListener<Money>(EventType.StarsValueChanged, value => UpdateStats());

                DescriptionPanel.gameObject.SetActive(false);

                _playerInventory = _inventoryFactory.CreatePlayerInventory();

                BuyToggle.onValueChanged.RemoveAllListeners();
                Buy_Toggle.onValueChanged.RemoveAllListeners();
                SellToggle.onValueChanged.RemoveAllListeners();
                BuyToggle.onValueChanged.AddListener(OnBlackMarketTabChanged);
                Buy_Toggle.onValueChanged.AddListener(OnArenaXmasTabChanged);
                SellToggle.onValueChanged.AddListener(OnSellTabChanged);

                BuyToggle.isOn = false;
                Buy_Toggle.isOn = false;
                SellToggle.isOn = false;
                BuyToggle.isOn = true;
            }

            private void OnBlackMarketTabChanged(bool selected)
            {
                if (!selected) return;
                _currentTab = Tab.BlackMarketAndStations;
                FilterGroup.Value = (int)Filter.All;
                BuildInventory();
                _selectedItem = null;
                ItemsGroup.SetAllTogglesOff();
                UpdateItems(true);
                UpdateStats();
            }

            private void OnArenaXmasTabChanged(bool selected)
            {
                if (!selected) return;
                _currentTab = Tab.ArenaAndXmas;
                FilterGroup.Value = (int)Filter.All;
                BuildInventory();
                _selectedItem = null;
                ItemsGroup.SetAllTogglesOff();
                UpdateItems(true);
                UpdateStats();
            }

            private void OnSellTabChanged(bool selected)
            {
                if (!selected) return;
                _currentTab = Tab.Sell;
                FilterGroup.Value = (int)Filter.All;
                _selectedItem = null;
                ItemsGroup.SetAllTogglesOff();
                UpdateItems(true);
                UpdateStats();
            }

            private void BuildInventory()
            {
                var items = new List<IProduct>();
                var furthestStar = System.Math.Max(0, _session.StarMap.FurthestVisitedStar);

                if (_currentTab == Tab.BlackMarketAndStations)
                {
                    var processedRegions = new HashSet<int>();

                    for (int starId = 0; starId <= furthestStar; starId++)
                    {
                        if (!_starData.IsVisited(starId)) continue;
                        if (_session.StarMap.GetEnemy(starId) == IStarMapData.Occupant.Unknown) continue;

                        var objects = _starData.GetObjects(starId);

                        if (objects.Contain(StarObjectType.BlackMarket))
                        {
                            try
                            {
                                var inventory = _inventoryFactory.CreateBlackMarketInventory(starId);
                                items.AddRange(inventory.Items);
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("Failed to create black market inventory for star " + starId + ": " + e.Message);
                            }
                        }

                        if (objects.Contain(StarObjectType.StarBase))
                        {
                            try
                            {
                                var region = _starData.GetRegion(starId);
                                if (region != null && processedRegions.Add(region.Id))
                                {
                                    var inventory = _inventoryFactory.CreateFactionInventory(region);
                                    items.AddRange(inventory.Items);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("Failed to create faction inventory for star " + starId + ": " + e.Message);
                            }
                        }
                    }

                    items = items
                        .GroupBy(item => item.Type.Id)
                        .Select(group => group.Count() == 1 ? group.First() : new MergedProduct(group.ToList()))
                        .ToList();
                }
                else
                {
                    var seenIds = new HashSet<string>();

                    for (int starId = 0; starId <= furthestStar; starId++)
                    {
                        if (!_starData.IsVisited(starId)) continue;
                        if (_session.StarMap.GetEnemy(starId) == IStarMapData.Occupant.Unknown) continue;

                        var objects = _starData.GetObjects(starId);

                        if (objects.Contain(StarObjectType.Arena))
                        {
                            try
                            {
                                var star = _starMap.GetStarById(starId);
                                var inventory = _inventoryFactory.CreateArenaInventory(star);
                                foreach (var item in inventory.Items)
                                {
                                    if (seenIds.Add(item.Type.Id))
                                        items.Add(item);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("Failed to create arena inventory for star " + starId + ": " + e.Message);
                            }
                        }

                        if (objects.Contain(StarObjectType.Xmas))
                        {
                            try
                            {
                                var inventory = _inventoryFactory.CreateSantaInventory(starId);
                                foreach (var item in inventory.Items)
                                {
                                    if (seenIds.Add(item.Type.Id))
                                        items.Add(item);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("Failed to create santa inventory for star " + starId + ": " + e.Message);
                            }
                        }
                    }

                    items = items.OrderBy(item => (long)item.Price.Amount).ToList();
                }

                _marketInventory = new CombinedInventory(items);
            }

            public void OnItemSelected(Common.InventoryItem item)
            {
                _selectedItem = item;
                _quantity = 1;
                ContentFiller.OnItemSelected(item);
                UpdateButtons();
            }

            public void OnItemDeselected()
            {
                _selectedItem = null;
                UpdateButtons();
            }

            public void MoreInfoButtonClicked()
            {
                if (_selectedItem != null)
                    _helper.ShowItemInfoWindow(_selectedItem.Product);
            }

            public void BuyButtonClicked()
            {
                if (_selectedItem == null) return;

                var selectedTypeId = _selectedItem.Product.Type.Id;
                var selectedIndex = ContentFiller.SelectedItemIndex;

                _selectedItem.Product.Buy(_quantity);
                _soundPlayer.Play(Sound);
                BuildInventory();
                UpdateItems(false, selectedTypeId, selectedIndex);
            }

            public void SellButtonClicked()
            {
                if (_selectedItem == null) return;
                _selectedItem.Product.Sell(_quantity);
                _soundPlayer.Play(Sound);
                UpdateItems();
            }

            public void SellJunkButtonClicked()
            {
                _helper.ShowConfirmation(_localization.GetString("$SellAllTrashConfirmation"), () =>
                {
                    var items = _playerInventory.Items.Where(IsJunk).ToArray();

                    foreach (var item in items)
                    {
                        var quantity = item.Quantity;
                        item.Sell(quantity);
                    }

                    _soundPlayer.Play(Sound);
                    UpdateItems();
                });
            }

            private static bool IsJunk(IProduct product)
            {
                if (product.Quantity <= 0)
                    return false;
                if (product.Type is ArtifactItem)
                    return false;

                return product.Type.Quality < ItemQuality.Common;
            }

            public void CloseButtonClicked()
            {
                GetComponent<IWindow>().Close();
            }

            public void OnQuantityChanged(float value)
            {
                _quantity = Mathf.RoundToInt(value);
                QuantityText.text = _quantity.ToString();

                if (_selectedItem != null)
                    PricePanel.Initialize(_selectedItem.Product.Type, _selectedItem.Product.Price * _quantity);
            }

            public void OnFilterSelected(int value)
            {
                UpdateItems();
            }

            private void UpdateStats()
            {
                MoneyText.text = _playerResources.Money.ToString();
#if IAP_DISABLED
                StarPanel.gameObject.SetActive(false);
#else
                StarPanel.gameObject.SetActive(true);
#endif
                StarText.text = _playerResources.Stars.ToString();
            }

            private void UpdateButtons()
            {
                int quantity = 0;

                if (_currentTab == Tab.Sell)
                {
                    BuyButton.gameObject.SetActive(false);
                    SellButton.gameObject.SetActive(true);
                    SellButton.interactable = _selectedItem != null;
                    _sellJunkButton.gameObject.SetActive(true);
                    _sellJunkButton.interactable = _playerInventory.Items.Any(IsJunk);
                    quantity = _selectedItem != null ? _selectedItem.Product.Quantity : 0;
                }
                else
                {
                    BuyButton.gameObject.SetActive(true);
                    SellButton.gameObject.SetActive(false);
                    _sellJunkButton.gameObject.SetActive(false);

                    BuyButton.interactable =
                        _selectedItem != null &&
                        _selectedItem.Product.Price.IsEnough(_playerResources) &&
                        _selectedItem.Product.Type.MaxItemsToConsume > 0;

                    if (_selectedItem != null)
                    {
                        var product = _selectedItem.Product;
                        if (product.Price.Amount > 0)
                            quantity = Mathf.Min(product.Quantity, product.Type.MaxItemsToConsume, product.Price.GetMaxItemsToWithdraw(_playerResources));
                        else
                            quantity = Mathf.Min(product.Quantity, product.Type.MaxItemsToConsume);
                    }
                }

                if (quantity > 1)
                {
                    QuantityPanel.gameObject.SetActive(true);
                    QuantitySlider.gameObject.SetActive(true);
                    QuantitySlider.maxValue = quantity;
                    QuantitySlider.value = 1;
                    QuantitySlider.onValueChanged.Invoke(1);
                }
                else
                {
                    QuantityPanel.gameObject.SetActive(false);
                    QuantitySlider.gameObject.SetActive(false);
                }

                UpdateItemDescription(_selectedItem != null ? _selectedItem.Product : null);
            }

            private void UpdateItemDescription(IProduct product)
            {
                if (product != null)
                {
                    DescriptionPanel.gameObject.SetActive(true);
                    Icon.sprite = _resourceLocator.GetSprite(product.Type.Icon);
                    Icon.color = product.Type.Color;
                    NameText.text = _localization.GetString(product.Type.Name);
                    DescrtiptionText.gameObject.SetActive(!string.IsNullOrEmpty(DescrtiptionText.text = product.Type.Description));
                    NameText.color = DescrtiptionText.color = Gui.Theme.UiTheme.Current.GetQualityColor(product.Type.Quality);
                    PricePanel.Initialize(product.Type, product.Price);
                }
                else
                {
                    DescriptionPanel.gameObject.SetActive(false);
                }
            }

            private int UpdateMarketItems(bool clearSelection = false, string selectTypeId = null, int fallbackIndex = -1)
            {
                var filter = (Filter)FilterGroup.Value;
                ContentFiller.InitializeItems(_marketInventory.Items.Where(item => item.Quantity > 0 && IsItemVisible(item)).OrderBy(item => item.Type.Id), false, clearSelection);

                if (selectTypeId != null)
                {
                    if (!ContentFiller.TrySelectItemByTypeId(selectTypeId))
                    {
                        if (fallbackIndex >= 0)
                            ContentFiller.SelectItem(fallbackIndex);
                    }
                }

                ItemList.RefreshContent();
                return ContentFiller.GetItemCount();
            }

            private int UpdatePlayerItems(bool clearSelection = false)
            {
                ContentFiller.InitializeItems(_playerInventory.Items.Where(IsItemVisible), true, clearSelection);
                ItemList.RefreshContent();
                return ContentFiller.GetItemCount();
            }

            private bool IsItemVisible(IProduct item)
            {
                var filter = (Filter)FilterGroup.Value;
                if (filter == Filter.All)
                    return true;

                if (item.Type is ComponentItem)
                {
                    var component = ((ComponentItem)item.Type).Component;
                    if (component.Data.DisplayCategory == ComponentCategory.Weapon)
                        return filter == Filter.Weapon;
                    else
                        return filter == Filter.Module;
                }

                if (item.Type is ShipItemBase)
                    return filter == Filter.Ship;

                if (item.Type is SatelliteItem)
                    return filter == Filter.Module;

                if (item.Type is ArtifactItem || item.Type is FuelItem)
                    return filter == Filter.Resource;

                return filter == Filter.Other;
            }

            private void UpdateItems(bool clearSelection = false, string selectTypeId = null, int fallbackIndex = -1)
            {
                var count = _currentTab == Tab.Sell ? UpdatePlayerItems(clearSelection) : UpdateMarketItems(clearSelection, selectTypeId, fallbackIndex);
                EmptyLabel.gameObject.SetActive(count == 0);

                _selectedItem = null;
                if (ItemsGroup.AnyTogglesOn())
                {
                    var first = ItemsGroup.ActiveToggles().FirstOrDefault();
                    foreach (var item in ItemsGroup.ActiveToggles().Skip(1))
                        item.isOn = false;
                    if (first != null)
                        first.onValueChanged.Invoke(true);
                }

                UpdateButtons();
                UpdateStats();
            }

            private void OnIapItemsChanged()
            {
                BuildInventory();
                UpdateItems();
            }

            private void Update()
            {
                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                    CloseButtonClicked();
            }

            private int _quantity;
            private Tab _currentTab;
            private IInventory _marketInventory;
            private IInventory _playerInventory;
            private Common.InventoryItem _selectedItem;

            private class CombinedInventory : IInventory
            {
                private readonly List<IProduct> _items;

                public CombinedInventory(List<IProduct> items)
                {
                    _items = items;
                }

                public void Refresh() { }

                public IEnumerable<IProduct> Items => _items;
            }

            private class MergedProduct : IProduct
            {
                private readonly List<IProduct> _products;

                public MergedProduct(List<IProduct> products)
                {
                    _products = products;
                }

                public IItemType Type => _products[0].Type;
                public int Quantity => _products.Sum(p => p.Quantity);
                public Price Price => _products[0].Price;

                public void Buy(int amount = 1)
                {
                    foreach (var product in _products)
                    {
                        if (amount <= 0) break;
                        var buyAmount = System.Math.Min(amount, product.Quantity);
                        if (buyAmount > 0)
                        {
                            product.Buy(buyAmount);
                            amount -= buyAmount;
                        }
                    }
                }

                public void Sell(int amount = 1)
                {
                    foreach (var product in _products)
                    {
                        if (amount <= 0) break;
                        var sellAmount = System.Math.Min(amount, product.Quantity);
                        if (sellAmount > 0)
                        {
                            product.Sell(sellAmount);
                            amount -= sellAmount;
                        }
                    }
                }
            }
        }
    }
}
