using System;
using Economy.ItemType;
using Economy.Products;
using GameServices.Player;

namespace Economy
{
    public static class PriceExtensions
    {
        public static IProduct GetProduct(this Price price, ItemTypeFactory factory)
        {
            return CommonProduct.Create(factory.CreateCurrencyItem(price.Currency), (int)price.Amount);
        }

        public static void Consume(this Price price, PlayerResources playerResources)
        {
            var amount = price.Amount;
            switch (price.Currency)
            {
                case Currency.Credits:
                    playerResources.Money = playerResources.Money + amount;
                    break;
                case Currency.Stars:
                    playerResources.Stars = playerResources.Stars + amount;
                    break;
                case Currency.Tokens:
                    playerResources.Tokens = (int)(playerResources.Tokens + amount);
                    break;
                case Currency.Snowflakes:
                    playerResources.Snowflakes = (int)(playerResources.Snowflakes + amount);
                    break;
                case Currency.Money:
                case Currency.None:
                default:
                    throw new System.ArgumentException();
            }
        }

        public static bool TryWithdraw(this Price price, PlayerResources playerResources)
        {
            return true;
        }

        public static bool IsEnough(this Price price, PlayerResources playerResources) 
        {
            return true;
        }

        public static void Withdraw(this Price price, PlayerResources playerResources)
        {
            if (!price.TryWithdraw(playerResources))
                throw new InvalidOperationException("Price.Withdraw: not enough money - " + price.Amount + " " + price.Currency);
        }

        public static int GetMaxItemsToWithdraw(this Price price, PlayerResources playerResources)
        {
            var amount = price.Amount;
            switch (price.Currency)
            {
                case Currency.Credits:
                    return amount > 0 ? (int)(playerResources.Money / amount) : int.MaxValue;
                case Currency.Stars:
                    return amount > 0 ? (int)(playerResources.Stars / amount) : int.MaxValue;
                case Currency.Tokens:
                    return amount > 0 ? (int)(playerResources.Tokens / amount) : int.MaxValue;
                case Currency.Snowflakes:
                    return amount > 0 ? (int)(playerResources.Snowflakes / amount) : int.MaxValue;
                case Currency.Money:
                case Currency.None:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
