# New Feature: Price Comparison Window

## What's New

### 📊 Compare Prices Button
A new "Compare" button has been added next to the mineral selector that opens a price comparison window showing ALL stations that sell the selected mineral.

## Features

### 1. **Station Price List**
- Shows every station that has pricing data for the selected mineral
- Displays station name, system, and sell price
- Ranked from best to worst (or sort however you like)

### 2. **Sorting Options**
- **Highest Price** - Shows best prices first (default)
- **Lowest Price** - Shows cheapest prices first
- **Station Name** - Alphabetical order

### 3. **Price Summary**
At the bottom, see:
- **Highest Price** (green) - Best station to sell at
- **Average Price** - Market average
- **Lowest Price** (red) - Worst station to sell at

## How to Use

### Opening the Comparison Window
1. Select a mineral (e.g., "Gold" or "Quantanium")
2. Click the **"📊 Compare"** button next to the dropdown
3. Price comparison window opens showing all stations

### Reading the Data
Each row shows:
```
Station Name          Price        Rank
System Name          (Yellow)      (#1, #2, etc.)
```

Example for Quantanium:
```
Orison                94,877 aUEC    #1
Stanton               94,378 aUEC    #2
Fallow Field          94,378 aUEC    #3
...
Grim HEX              76,401 aUEC    #14
```

### Finding the Best Deal
- **Green "Highest Price"** = Where to sell for maximum profit
- Look at the top of the list for best stations
- Red "Lowest Price" shows where NOT to sell

## Installation

### New Files
1. **PriceComparisonWindow.xaml** - The window design
2. **PriceComparisonWindow.xaml.cs** - The window logic

### Updated Files
1. **CalculatorWindow.xaml** - Added Compare button
2. **CalculatorWindow.xaml.cs** - Added button handler

### Steps
1. Add `PriceComparisonWindow.xaml` to your project
2. Add `PriceComparisonWindow.xaml.cs` to your project
3. Replace `CalculatorWindow.xaml` with updated version
4. Replace `CalculatorWindow.xaml.cs` with updated version
5. Build and run!

## Usage Examples

### Example 1: Finding Best Quantanium Prices
1. Select "Quantanium" from dropdown
2. Click "📊 Compare"
3. See that **Orison pays 94,877 aUEC** (highest)
4. See that **Grim HEX only pays 76,401 aUEC** (lowest)
5. **Difference: 18,476 aUEC per SCU!**

### Example 2: Gold Trading
1. Select "Gold"
2. Click "📊 Compare"
3. Compare prices across all stations
4. Plan your trade route to maximize profit

### Example 3: Quick Price Check
- Need to know current Copper prices?
- Select "Copper" → Click "Compare"
- Instantly see all station prices

## Technical Details

### Data Source
- Pulls from the same UEX Corp API data
- Uses `commodities_prices_all` endpoint
- Shows only stations with reported prices

### Mineral Name Mapping
- Handles the "Quantanium" → "Quantainium" API difference automatically
- Works for all 16 minerals in your calculator

### Performance
- Loads instantly (data already in memory)
- No additional API calls needed
- Can open/close window multiple times

## Troubleshooting

**"Price data is still loading" message:**
- Wait a few seconds after app starts for data to load
- Indicator text shows "X commodities with live prices" when ready

**Empty price list:**
- Some minerals may not be sold at many stations
- Rare materials have fewer trading locations
- Try a different mineral

**Incorrect prices:**
- Prices come from UEX Corp community data
- Reflects player-reported prices
- May not update instantly with game patches

## Future Enhancements

Could add:
- **Export to CSV** - Save comparison table
- **Trade Route Calculator** - Show best buy/sell combinations
- **Price History Graph** - Track changes over time
- **Filter by System** - Show only Stanton or Pyro stations
- **Distance Calculator** - Factor in travel time
- **Profit Calculator** - Show profit margins for trading

## Tips

### Maximizing Profit
1. Compare prices before each mining/trading trip
2. Note the highest paying station for your cargo
3. Plan your route to end at that station
4. Can make **thousands more per SCU** by choosing wisely!

### Quick Reference
- Keep window open while playing
- Alt-tab to check prices mid-game
- Bookmark favorite high-paying stations

### Trading Strategy
- Buy at low price stations
- Sell at high price stations  
- Use comparison window to find arbitrage opportunities
- Especially useful for Quantanium (biggest price variation!)

## Summary

The Price Comparison feature turns your calculator into a powerful trading tool. Instead of just calculating cargo value, you can now:
- ✅ Find the best stations to sell at
- ✅ Compare prices across the entire system
- ✅ Make data-driven trading decisions
- ✅ Maximize profit on every haul

Happy trading! 🚀
