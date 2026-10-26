# Portfolio Rebalance

A small .NET library that models a stock portfolio with a target allocation and
calculates the buy and sell orders needed to rebalance it.

## Run it

Requires the .NET 10 SDK.

```bash
dotnet test
```

## Usage

```csharp
var meta = new Stock("META", 500m);
var aapl = new Stock("AAPL", 200m);

var portfolio = new Portfolio(
    positions:  [new Position(meta, 10), new Position(aapl, 10)],        // $5,000 + $2,000
    allocation: [new AllocatedStock(meta, 0.40m), new AllocatedStock(aapl, 0.60m)]);

var orders = portfolio.Rebalance();
// Sell 4.4 META ($2,200)
// Buy  11  AAPL ($2,200)
```

## Model

| Type | Responsibility |
|---|---|
| `Stock` | Ticker symbol and `CurrentPrice()` (last available price). |
| `Position` | A stock plus the quantity held. A `Stock` alone does not say how many shares you own. |
| `AllocatedStock` | A stock plus its target weight (a fraction from 0 to 1). |
| `Portfolio` | Holds positions and the allocation, validates them, and calculates `Rebalance()`. |
| `Order` | One suggested trade: symbol, side, shares, price used, and amount. |

## How the rebalance works

The current total value is redistributed according to the target weights:

```
totalValue  = Σ quantity × price
targetValue = totalValue × weight
difference  = targetValue − currentValue
shares      = |difference| / price          (difference > 0 → Buy, < 0 → Sell)
```

The rebalance walks over every stock that is **either held or allocated**, so:

- a stock held but not in the allocation has weight 0 and is **sold completely**;
- a stock in the allocation but not held has quantity 0 and is **bought from zero**.

No money enters or leaves the portfolio, so the value bought always equals the value sold.

## Design decisions

- **`Rebalance()` is side-effect free.** It returns suggested orders and never changes the
  portfolio. Executing trades belongs to another layer (broker, settlement, failures).
- **`decimal` instead of `double`** for money and quantities, to avoid floating-point errors.
- **Fractional shares are allowed**, so share counts are not rounded.
- **Prices are read once per rebalance.** If a price changed during the calculation, the
  orders would no longer net to zero.
- **Exact quantity on full sells.** When a stock's target weight is 0, the order sells the
  exact quantity held rather than `value / price`, which could leave a tiny residue.
- **Sells are listed before buys**, because sells provide the cash that buys need.
- **`minTradeAmount` (optional)** skips trades whose amount is at or below the threshold,
  so tiny trades that would cost more in fees than they fix are not generated.

## Validation and edge cases

| Case | Behavior |
|---|---|
| Weights do not add up to 100% (±0.01%) | `ArgumentException`. Rejected rather than normalized, so configuration mistakes are not silently hidden. |
| Weight < 0 or > 1 | `ArgumentOutOfRangeException` |
| Empty allocation | `ArgumentException` |
| Duplicate stock in positions or allocation | `ArgumentException` |
| Same symbol mapped to two different `Stock` objects | `ArgumentException`. One source of truth for each price. |
| Price ≤ 0 | `ArgumentOutOfRangeException`. The calculation divides by the price. |
| Negative quantity (short) | `ArgumentOutOfRangeException`. Out of scope. |
| Portfolio already balanced | Empty list |
| Portfolio with no value (empty or all zero quantities) | Empty list. Investing new money is a different operation. |

## Out of scope

These would matter in production but are intentionally not modeled:

- Cash balances and deposits or withdrawals (for example, rebalancing by investing a new deposit instead of selling).
- Transaction fees, taxes on realized gains, and minimum order sizes.
- Rounding to whole shares or broker lot sizes, and how to handle the resulting cash remainder.
- Stale or out-of-sync price data across stocks.
