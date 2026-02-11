namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IWindowService
    {
        void ShowPricesWindow();
        void ShowCalculatorWindow();
        void ShowHaulingPricesWindow();
        void ShowHaulingCalculatorWindow();
        void ShowRefineryCalculatorWindow();
        void ShowLocationWindow(string name, bool isMineral, bool isAsteroid, bool isRoc);
    }
}
