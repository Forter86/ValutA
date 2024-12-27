using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace CurrencyConverterMaui
{
    public partial class MainPage : ContentPage
    {
        private static readonly string ApiUrl = "https://www.cbr-xml-daily.ru/archive/";
        private static Dictionary<string, decimal> _currentRates;
        private bool _isUpdating;

        public MainPage()
        {
            InitializeComponent();
            AmountFromEntry.Focused += (s, e) => AmountFromEntry.CursorPosition = AmountFromEntry.Text?.Length ?? 0;
            AmountToEntry.Focused += (s, e) => AmountToEntry.CursorPosition = AmountToEntry.Text?.Length ?? 0;
        }

        private async void AmountFromEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            await UpdateConversion(true);
        }

        private async void AmountToEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            await UpdateConversion(false);
        }


        private async Task UpdateConversion(bool isFromAmountChange)
        {
            _isUpdating = true;
            string dateInput = ConvertDateEntry.Text;
            string fromCurrency = FromCurrencyEntry.Text?.ToUpper();
            string toCurrency = ToCurrencyEntry.Text?.ToUpper();


            if (string.IsNullOrWhiteSpace(dateInput) || string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency))
            {
                AmountFromEntry.Text = "";
                AmountToEntry.Text = "";
                _isUpdating = false;
                return;
            }

            if (!DateTime.TryParseExact(dateInput, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime selectedDate))
            {
                AmountFromEntry.Text = "";
                AmountToEntry.Text = "";
                _isUpdating = false;
                return;
            }
            _currentRates = await GetCurrencyRates(selectedDate);
            if (_currentRates == null || _currentRates.Count == 0)
            {
                AmountFromEntry.Text = "";
                AmountToEntry.Text = "";
                _isUpdating = false;
                return;
            }


            if (isFromAmountChange)
            {
                if (decimal.TryParse(AmountFromEntry.Text, out decimal amount))
                {
                    decimal result = ConvertCurrency(amount, fromCurrency, toCurrency, _currentRates);
                    if (result != -1)
                    {
                        AmountToEntry.Text = $"{result:N2}";
                    }
                    else
                    {
                        AmountToEntry.Text = "";
                    }
                }
                else
                {
                    AmountToEntry.Text = "";
                }
            }
            else
            {
                if (decimal.TryParse(AmountToEntry.Text, out decimal amount))
                {
                    decimal result = ConvertCurrency(amount, toCurrency, fromCurrency, _currentRates);
                    if (result != -1)
                    {
                        AmountFromEntry.Text = $"{result:N2}";
                    }
                    else
                    {
                        AmountFromEntry.Text = "";
                    }
                }
                else
                {
                    AmountFromEntry.Text = "";
                }
            }

            _isUpdating = false;
        }


        public static decimal ConvertCurrency(decimal amount, string fromCurrency, string toCurrency, Dictionary<string, decimal> rates)
        {
            if (rates.ContainsKey(fromCurrency) && rates.ContainsKey(toCurrency))
            {
                return amount / (rates[toCurrency] / rates[fromCurrency]);
            }
            return -1;
        }

        public static async Task<Dictionary<string, decimal>> GetCurrencyRates(DateTime date)
        {
            Dictionary<string, decimal> rates = null;
            DateTime currentDate = date;

            for (int i = 0; i < 10; i++)
            {
                rates = await GetRatesForDate(currentDate);
                if (rates != null)
                {
                    if (currentDate != date)
                        System.Diagnostics.Debug.WriteLine($"Курсы валют не найдены на {date:dd-MM-yyyy}. Показаны данные на {currentDate:dd-MM-yyyy}.");
                    return rates;
                }
                currentDate = currentDate.AddDays(-1);
            }
            return null;
        }

        private static async Task<Dictionary<string, decimal>> GetRatesForDate(DateTime date)
        {
            string dateUrlPart = date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            string fullUrl = $"{ApiUrl}{dateUrlPart}/daily_json.js";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(fullUrl);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();

                    var json = JsonDocument.Parse(responseBody);
                    var valutes = json.RootElement.GetProperty("Valute");

                    Dictionary<string, decimal> rates = new Dictionary<string, decimal>();
                    foreach (var valute in valutes.EnumerateObject())
                    {
                        decimal value = valute.Value.GetProperty("Value").GetDecimal();
                        string charCode = valute.Name;
                        rates.Add(charCode, value);
                    }
                    rates.Add("RUB", 1);
                    return rates;
                }
                catch (HttpRequestException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка HTTP-запроса: {ex.Message}");
                    return null;
                }
                catch (JsonException)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
                    return null;
                }
            }
        }
    }
}