namespace LocalCrypto.Core;

public static class PortfolioCalculator
{
    public static PortfolioSnapshot Calculate(IEnumerable<LedgerTransaction> transactions, string baseCurrency = "USDT")
    {
        var ordered = transactions
            .OrderBy(transaction => transaction.ExecutedAt)
            .ThenBy(transaction => transaction.Id, StringComparer.Ordinal)
            .ToList();

        var states = new Dictionary<string, PositionState>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        decimal totalFees = 0m;

        foreach (var transaction in ordered)
        {
            if (!TryValidate(transaction, out var warning))
            {
                warnings.Add(warning);
                continue;
            }

            var state = GetState(states, transaction);
            totalFees += transaction.QuoteFee;

            if (transaction.Side == TradeSide.Buy)
            {
                state.Quantity += transaction.Quantity;
                state.InvestedCost += transaction.GrossAmount + transaction.QuoteFee;
                state.Fees += transaction.QuoteFee;
                continue;
            }

            if (state.Quantity <= 0m)
            {
                warnings.Add($"{transaction.Symbol}: vente ignoree car aucune position n'existe avant {transaction.ExecutedAt:yyyy-MM-dd}.");
                continue;
            }

            var soldQuantity = Math.Min(transaction.Quantity, state.Quantity);
            if (soldQuantity < transaction.Quantity)
            {
                warnings.Add($"{transaction.Symbol}: vente partielle calculee sur {soldQuantity}, la quantite demandee depasse la position.");
            }

            var averageCost = state.AverageCost;
            var costBasis = averageCost * soldQuantity;
            var proceeds = soldQuantity * transaction.UnitPrice - transaction.QuoteFee;

            state.Quantity -= soldQuantity;
            state.InvestedCost -= costBasis;
            state.RealizedPnl += proceeds - costBasis;
            state.Fees += transaction.QuoteFee;

            if (state.Quantity == 0m)
            {
                state.InvestedCost = 0m;
            }
        }

        var positions = states.Values
            .Where(state => state.Quantity > 0m)
            .OrderBy(state => state.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(state => new PositionSnapshot(
                state.Symbol,
                state.AssetName,
                state.Quantity,
                state.AverageCost,
                state.InvestedCost,
                state.RealizedPnl,
                state.Fees,
                state.QuoteCurrency))
            .ToList();

        return new PortfolioSnapshot(
            baseCurrency,
            positions.Sum(position => position.InvestedCost),
            states.Values.Sum(state => state.RealizedPnl),
            totalFees,
            positions,
            ordered,
            warnings);
    }

    private static PositionState GetState(Dictionary<string, PositionState> states, LedgerTransaction transaction)
    {
        if (states.TryGetValue(transaction.Symbol, out var state))
        {
            return state;
        }

        state = new PositionState(
            transaction.Symbol,
            string.IsNullOrWhiteSpace(transaction.AssetName) ? transaction.Symbol : transaction.AssetName,
            transaction.QuoteCurrency);
        states.Add(transaction.Symbol, state);
        return state;
    }

    private static bool TryValidate(LedgerTransaction transaction, out string warning)
    {
        if (string.IsNullOrWhiteSpace(transaction.Symbol))
        {
            warning = "Transaction ignoree: symbole vide.";
            return false;
        }

        if (transaction.Quantity <= 0m)
        {
            warning = $"{transaction.Symbol}: transaction ignoree car la quantite doit etre positive.";
            return false;
        }

        if (transaction.UnitPrice <= 0m)
        {
            warning = $"{transaction.Symbol}: transaction ignoree car le prix unitaire doit etre positif.";
            return false;
        }

        if (transaction.FeeAmount < 0m)
        {
            warning = $"{transaction.Symbol}: transaction ignoree car les frais ne peuvent pas etre negatifs.";
            return false;
        }

        warning = string.Empty;
        return true;
    }

    private sealed class PositionState
    {
        public PositionState(string symbol, string assetName, string quoteCurrency)
        {
            Symbol = symbol.ToUpperInvariant();
            AssetName = assetName;
            QuoteCurrency = quoteCurrency.ToUpperInvariant();
        }

        public string Symbol { get; }

        public string AssetName { get; }

        public string QuoteCurrency { get; }

        public decimal Quantity { get; set; }

        public decimal InvestedCost { get; set; }

        public decimal RealizedPnl { get; set; }

        public decimal Fees { get; set; }

        public decimal AverageCost => Quantity == 0m ? 0m : InvestedCost / Quantity;
    }
}
