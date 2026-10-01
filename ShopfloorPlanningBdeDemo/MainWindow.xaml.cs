using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace ShopfloorPlanningBdeDemo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly string _databasePath;
    private static readonly HttpClient _httpClient = new();
    private bool _isEnglish = false;

    public MainWindow()
    {
        InitializeComponent();

        _databasePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "shopfloor_bde.db");

        InitializeDatabase();
        LoadFeedbackHistory();
        ShowHomeView();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS FeedbackRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                OrderNumber TEXT NOT NULL,
                GoodQuantity INTEGER NOT NULL,
                ScrapQuantity INTEGER NOT NULL,
                Comment TEXT
            );
            """;

        command.ExecuteNonQuery();
    }

    private void SaveFeedback_Click(object sender, RoutedEventArgs e)
    {
        string orderNumber = OrderNumberTextBox.Text.Trim();
        string goodQuantityText = GoodQuantityTextBox.Text.Trim();
        string scrapQuantityText = ScrapQuantityTextBox.Text.Trim();
        string comment = CommentTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            MessageBox.Show(
                _isEnglish ? "Please enter an order number." : "Bitte geben Sie eine Auftragsnummer ein.",
                _isEnglish ? "Input error" : "Eingabefehler",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        bool goodIsNumber = int.TryParse(goodQuantityText, out int goodQuantity);
        bool scrapIsNumber = int.TryParse(scrapQuantityText, out int scrapQuantity);

        if (!goodIsNumber || !scrapIsNumber)
        {
            MessageBox.Show(
                _isEnglish
                    ? "Please enter valid numbers for good quantity and scrap."
                    : "Bitte geben Sie gültige Zahlen für Gutmenge und Ausschuss ein.",
                _isEnglish ? "Input error" : "Eingabefehler",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        SaveFeedbackRecord(orderNumber, goodQuantity, scrapQuantity, comment);
        UpdateFeedbackDisplay(orderNumber, goodQuantity, scrapQuantity);
        LoadFeedbackHistory();

        MessageBox.Show(
            _isEnglish
                ? $"Order {orderNumber} was saved.\nGood quantity: {goodQuantity}\nScrap: {scrapQuantity}"
                : $"Auftrag {orderNumber} wurde gespeichert.\nGutmenge: {goodQuantity}\nAusschuss: {scrapQuantity}",
            _isEnglish ? "BDE Feedback" : "BDE Rückmeldung",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SaveFeedbackRecord(
        string orderNumber,
        int goodQuantity,
        int scrapQuantity,
        string comment)
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FeedbackRecords
            (CreatedAt, OrderNumber, GoodQuantity, ScrapQuantity, Comment)
            VALUES ($createdAt, $orderNumber, $goodQuantity, $scrapQuantity, $comment);
            """;

        command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("$orderNumber", orderNumber);
        command.Parameters.AddWithValue("$goodQuantity", goodQuantity);
        command.Parameters.AddWithValue("$scrapQuantity", scrapQuantity);
        command.Parameters.AddWithValue("$comment", comment);

        command.ExecuteNonQuery();
    }

    private void LoadFeedbackHistory()
    {
        FeedbackHistoryListBox.Items.Clear();

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CreatedAt, OrderNumber, GoodQuantity, ScrapQuantity
            FROM FeedbackRecords
            ORDER BY Id DESC
            LIMIT 20;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            string createdAt = reader.GetString(0);
            string orderNumber = reader.GetString(1);
            int goodQuantity = reader.GetInt32(2);
            int scrapQuantity = reader.GetInt32(3);

            string time = DateTime.TryParse(createdAt, out DateTime parsedDate)
                ? parsedDate.ToString("HH:mm")
                : createdAt;

            string historyLine = _isEnglish
                ? $"{time} | {orderNumber} | Good: {goodQuantity} | Scrap: {scrapQuantity}"
                : $"{time} | {orderNumber} | Gut: {goodQuantity} | Ausschuss: {scrapQuantity}";

            FeedbackHistoryListBox.Items.Add(historyLine);
        }
    }

    private async void GenerateAiReport_Click(object sender, RoutedEventArgs e)
    {
        GenerateAiReportButton.IsEnabled = false;
        AiReportTextBlock.Text = _isEnglish
            ? "Generating AI report..."
            : "KI-Bericht wird erstellt...";

        try
        {
            string feedbackData = LoadFeedbackDataForAi();

            if (string.IsNullOrWhiteSpace(feedbackData))
            {
                AiReportTextBlock.Text = _isEnglish
                    ? "No feedback records found. Please save production feedback first."
                    : "Keine Rückmeldungen gefunden. Bitte zuerst Produktionsdaten speichern.";

                return;
            }

            string report = await GenerateAiReportAsync(feedbackData);
            AiReportTextBlock.Text = report;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                _isEnglish ? "AI report error" : "Fehler bei KI-Bericht",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            GenerateAiReportButton.IsEnabled = true;
        }
    }

    private string LoadFeedbackDataForAi()
    {
        var builder = new StringBuilder();

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CreatedAt, OrderNumber, GoodQuantity, ScrapQuantity, Comment
            FROM FeedbackRecords
            ORDER BY Id DESC
            LIMIT 20;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            string createdAt = reader.GetString(0);
            string orderNumber = reader.GetString(1);
            int goodQuantity = reader.GetInt32(2);
            int scrapQuantity = reader.GetInt32(3);
            string comment = reader.IsDBNull(4) ? "" : reader.GetString(4);

            builder.AppendLine(
                $"Time: {createdAt}, Order: {orderNumber}, Good: {goodQuantity}, Scrap: {scrapQuantity}, Comment: {comment}");
        }

        return builder.ToString();
    }

    private async Task<string> GenerateAiReportAsync(string feedbackData)
    {
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OPENAI_API_KEY is missing. Please set the API key as a Windows environment variable.");
        }

        string prompt = $"""
            You are an assistant for a German industrial shopfloor software system.

            Analyze the following BDE feedback records from a WPF demo application.
            Focus on production output, scrap quantity, quality risks and practical recommendations.

            Write a short professional report in English.
            Use clear bullet points.
            Do not invent machine data that is not provided.

            Data:
            {feedbackData}
            """;

        var requestBody = new
        {
            model = "gpt-4.1-mini",
            input = prompt
        };

        string json = JsonSerializer.Serialize(requestBody);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/responses");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        string responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"API request failed: {response.StatusCode}\n{responseText}");
        }

        return ExtractResponseText(responseText);
    }

    private string ExtractResponseText(string responseText)
    {
        using JsonDocument document = JsonDocument.Parse(responseText);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("output_text", out JsonElement outputText))
        {
            return outputText.GetString() ?? "No report text returned.";
        }

        if (root.TryGetProperty("output", out JsonElement outputArray))
        {
            foreach (JsonElement outputItem in outputArray.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out JsonElement contentArray))
                {
                    continue;
                }

                foreach (JsonElement contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out JsonElement textElement))
                    {
                        return textElement.GetString() ?? "No report text returned.";
                    }
                }
            }
        }

        return "No report text returned.";
    }

    private void UpdateFeedbackDisplay(string orderNumber, int goodQuantity, int scrapQuantity)
    {
        LastFeedbackTextBlock.Text = _isEnglish
            ? $"Order {orderNumber}: Good quantity {goodQuantity}, Scrap {scrapQuantity}"
            : $"Auftrag {orderNumber}: Gutmenge {goodQuantity}, Ausschuss {scrapQuantity}";
    }

    private void HomeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHomeView();
    }

    private void BdeNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowBdeView();
    }

    private void DatabaseNavButton_Click(object sender, RoutedEventArgs e)
    {
        LoadFeedbackHistory();
        ShowDatabaseView();
    }

    private void AiNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowAiView();
    }

    private void ShowHomeView()
    {
        HomeView.Visibility = Visibility.Visible;
        BdeView.Visibility = Visibility.Collapsed;
        DatabaseView.Visibility = Visibility.Collapsed;
        AiView.Visibility = Visibility.Collapsed;

        SetActiveNavigation(HomeNavButton);
    }

    private void ShowBdeView()
    {
        HomeView.Visibility = Visibility.Collapsed;
        BdeView.Visibility = Visibility.Visible;
        DatabaseView.Visibility = Visibility.Collapsed;
        AiView.Visibility = Visibility.Collapsed;

        SetActiveNavigation(BdeNavButton);
    }

    private void ShowDatabaseView()
    {
        HomeView.Visibility = Visibility.Collapsed;
        BdeView.Visibility = Visibility.Collapsed;
        DatabaseView.Visibility = Visibility.Visible;
        AiView.Visibility = Visibility.Collapsed;

        SetActiveNavigation(DatabaseNavButton);
    }

    private void ShowAiView()
    {
        HomeView.Visibility = Visibility.Collapsed;
        BdeView.Visibility = Visibility.Collapsed;
        DatabaseView.Visibility = Visibility.Collapsed;
        AiView.Visibility = Visibility.Visible;

        SetActiveNavigation(AiNavButton);
    }

    private void SetActiveNavigation(Button activeButton)
    {
        HomeNavButton.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        BdeNavButton.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        DatabaseNavButton.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));
        AiNavButton.Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235));

        activeButton.Foreground = new SolidColorBrush(Color.FromRgb(255, 196, 0));
    }

    private void ToggleLanguage_Click(object sender, RoutedEventArgs e)
    {
        _isEnglish = !_isEnglish;
        ApplyLanguage();
        LoadFeedbackHistory();
    }

    private void ApplyLanguage()
    {
        if (_isEnglish)
        {
            MainSubtitleTextBlock.Text = "Demo for work orders, machine status and shopfloor data collection";
            LanguageButton.Content = "DE";

            OrdersTitleTextBlock.Text = "Manufacturing Orders";
            OrdersSubtitleTextBlock.Text = "Live order queue";

            PlanningSubtitleTextBlock.Text = "Machine and workstation overview";

            BdeTitleTextBlock.Text = "BDE Feedback";
            BdeSubtitleTextBlock.Text = "Shopfloor data collection";
            EmployeeLabelTextBlock.Text = "Employee";
            OrderLabelTextBlock.Text = "Order";
            GoodLabelTextBlock.Text = "Good quantity";
            ScrapLabelTextBlock.Text = "Scrap";
            CommentLabelTextBlock.Text = "Comment";
            SaveFeedbackButton.Content = "Save feedback";
            LastFeedbackTitleTextBlock.Text = "Last feedback";

            AiReportTitleTextBlock.Text = "AI Report Analysis";
            AiReportSubtitleTextBlock.Text = "LLM-based analysis of the latest BDE feedback records";
            GenerateAiReportButton.Content = "Generate AI Report";

            if (LastFeedbackTextBlock.Text == "Noch keine Rückmeldung gespeichert.")
            {
                LastFeedbackTextBlock.Text = "No feedback saved yet.";
            }

            if (AiReportTextBlock.Text == "Noch kein KI-Bericht erstellt.")
            {
                AiReportTextBlock.Text = "No AI report generated yet.";
            }

            FooterTextBlock.Text = "Portfolio demo: WPF, C#, production planning, BDE, SQLite, LLM report analysis";
        }
        else
        {
            MainSubtitleTextBlock.Text = "Demo für Fertigungsaufträge, Maschinenstatus und Betriebsdatenerfassung";
            LanguageButton.Content = "EN";

            OrdersTitleTextBlock.Text = "Fertigungsaufträge";
            OrdersSubtitleTextBlock.Text = "Aktive Auftragsliste";

            PlanningSubtitleTextBlock.Text = "Maschinen- und Arbeitsplatzübersicht";

            BdeTitleTextBlock.Text = "BDE Rückmeldung";
            BdeSubtitleTextBlock.Text = "Betriebsdatenerfassung";
            EmployeeLabelTextBlock.Text = "Mitarbeiter";
            OrderLabelTextBlock.Text = "Auftrag";
            GoodLabelTextBlock.Text = "Gutmenge";
            ScrapLabelTextBlock.Text = "Ausschuss";
            CommentLabelTextBlock.Text = "Kommentar";
            SaveFeedbackButton.Content = "Rückmeldung speichern";
            LastFeedbackTitleTextBlock.Text = "Letzte Rückmeldung";

            AiReportTitleTextBlock.Text = "KI-Berichtsanalyse";
            AiReportSubtitleTextBlock.Text = "LLM-Analyse der letzten BDE-Rückmeldungen";
            GenerateAiReportButton.Content = "KI-Bericht erstellen";

            if (LastFeedbackTextBlock.Text == "No feedback saved yet.")
            {
                LastFeedbackTextBlock.Text = "Noch keine Rückmeldung gespeichert.";
            }

            if (AiReportTextBlock.Text == "No AI report generated yet.")
            {
                AiReportTextBlock.Text = "Noch kein KI-Bericht erstellt.";
            }

            FooterTextBlock.Text = "Portfolio-Demo: WPF, C#, Produktionsplanung, BDE, SQLite, LLM-Berichtsanalyse";
        }
    }
}