using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MyBrowser;
// Partial indica che un classe viene spezzata in due , una parte viene scritta in C#, una viene generata da Avalonia leggendo l'axaml
public partial class MainWindow : Window
{
    // HttpClient statico: riuso e socket pooling corretti (evita leak di socket)
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        // default, rimane vuoto per eventuali implementazioni future
    });

    // Token per annullare una navigazione quando ne parte un’altra
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        // Carica lo XAML e collega i controlli con x:Name
        InitializeComponent();

        // URL di test 
        UrlBox.Text = "https://example.com";
    }

    // Go button: Avalonia chiama il metodo OnGoCliCked() che chiama NavigateAsync().
    // La call stack : Button → Avalonia event system → OnGoClicked → NavigateAsync

    private async void OnGoClicked(object? sender, RoutedEventArgs e)
        => await NavigateAsync();

    // Invio nel TextBox dell’URL
    private async void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await NavigateAsync();
    }

    // Esegue la navigazione e popola l’area contenuti
    private async Task NavigateAsync()
    {
        // Legge l’URL e rimuove spazi
        var raw = UrlBox.Text?.Trim();

        // Validazione base campo vuoto
        if (string.IsNullOrWhiteSpace(raw))
        {
            SetStatus("Inserisci un URL.");
            ContentArea.Text = "";
            return;
        }

        // Se l’utente scrive example.com, aggiunge https://
        var url = NormalizeUrl(raw);

        // Validazione URL ben formato
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            SetStatus("URL non valido.");
            ContentArea.Text = "";
            return;
        }

        // Cancella eventuale navigazione precedente
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            // Disabilita input per evitare richieste concorrenti
            SetBusy(true);
            SetStatus($"Carico: {uri}");

            // Feedback immediato
            ContentArea.Text = "Loading...";

            // Prepara richiesta HTTP GET
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.UserAgent.ParseAdd("MiniBrowser/0.1 (+Avalonia)");

            // Esegue richiesta e legge solo gli header prima
            using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            res.EnsureSuccessStatusCode();

            // Legge tutto l’HTML
            var html = await res.Content.ReadAsStringAsync(ct);

            // Mostra risultato
            ContentArea.Text = html;
            SetStatus($"OK - {html.Length} caratteri");
        }
        catch (OperationCanceledException)
        {
            // Navigazione annullata non è un errore!
            SetStatus("Navigazione annullata.");
        }
        catch (HttpRequestException ex)
        {
            // Errori HTTP 
            SetStatus("Errore HTTP.");
            ContentArea.Text = ex.Message;
        }
        catch (Exception ex)
        {
            // Qualsiasi altro errore imprevisto
            SetStatus("Errore inatteso.");
            ContentArea.Text = ex.ToString();
        }
        finally
        {
            // Riabilita input
            SetBusy(false);
        }
    }

    // Normalizza l’URL se manca lo schema, assume https
    private static string NormalizeUrl(string raw)
    {
        if (!raw.Contains("://", StringComparison.Ordinal))
            return "https://" + raw;

        return raw;
    }

    // Aggiorna la barra di stato
    private void SetStatus(string message) => StatusText.Text = message;

    // Abilita/disabilita input durante una richiesta
    private void SetBusy(bool busy)
    {
        GoButton.IsEnabled = !busy;
        UrlBox.IsEnabled = !busy;
    }
//Sintesi :

//    Avvio app

//App.axaml.cs → crea new MainWindow() → Avalonia mostra la finestra

//Quando si crea la finestra

//MainWindow() → InitializeComponent() → Avalonia crea e collega controlli

//Quando l’utente interagisce

//UI(Button / TextBox) → sistema eventi Avalonia → chiama i metodi(OnGoClicked, OnUrlKeyDown)

//Quando si naviga

//Code → HttpClient → rete → server → risposta → code → UI
}
