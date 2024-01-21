using Azure;
using Azure.AI.Language.Conversations;
using Azure.Core;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using OneOf;

namespace HealthHackSgSeedFundUsPlease.Components.Pages;

public partial class Home
{
    string languageKey;
    string languageEndpoint;
    string speechKey;
    string speechRegion;
    string cluProjectName;
    string cluDeploymentName;

    [Inject] private ILogger<Home> Logger { get; set; }

    public Home()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("wwwroot/appsettings.Development.json")
            .AddEnvironmentVariables()
            .Build();
        languageKey = configuration["Ai:LanguageKey"];
        languageEndpoint = configuration["Ai:LanguageEndpoint"];
        speechKey = configuration["Ai:SpeechKey"];
        speechRegion = configuration["Ai:SpeechRegion"];
        cluProjectName = configuration["Ai:CluProjectName"];
        cluDeploymentName = configuration["Ai:CluDeploymentName"];
    }
    
    private string input = "";
    private string translated = "";
    private string intent = "";

    private bool isRecording = false;
    private string recordingClass => isRecording ? "visible" : "invisible";

    [Inject]
    public NavigationManager NavigationManager { get; set; }

	public async Task Voice()
    {
        isRecording = true;
        // TODO: allow user to set incoming language: zh-CN, ms-MY, ta-IN
        var speechRecognitionLanguage = "zh-CN";
        var (original, translated) = await GetOriginalAndTranslatedAsync();
        intent = await OutputPredictionAsync();
        input = original;
        this.translated = translated;

        if (!string.IsNullOrEmpty(intent))
        {
            NavigateBasedOnIntent(intent);
        }

        return;

        async Task<(string Original, string Translated)> GetOriginalAndTranslatedAsync()
        {
            if (speechRecognitionLanguage == "en-US")
            {
                var toReturn = (await GetSpeechRecognitionResultAsync()).AsT0;
                return (toReturn, toReturn);
            }
            else
            {
                return (await GetTranslatedSpeechRecognitionResultAsync()).AsT0;
            }
        }

        async Task<OneOf<string, Error>> GetSpeechRecognitionResultAsync()
        {
            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = speechRecognitionLanguage;

            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

            Console.WriteLine("Speak into your microphone.");
            var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();

            switch (speechRecognitionResult.Reason)
            {
                case ResultReason.RecognizedSpeech:
                    Console.WriteLine($"RECOGNIZED: Text={speechRecognitionResult.Text}");
                    return speechRecognitionResult.Text;
                case ResultReason.NoMatch:
                    Console.WriteLine("NOMATCH: Speech could not be recognized.");
                    return new Error();
                case ResultReason.Canceled:
                    var cancellation = CancellationDetails.FromResult(speechRecognitionResult);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Console.WriteLine("CANCELED: Did you set the speech resource key and region values?");
                    }

                    return new Error();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        async Task<OneOf<(string Original, string Translated), Error>> GetTranslatedSpeechRecognitionResultAsync()
        {
            var speechTranslationConfig = SpeechTranslationConfig.FromSubscription(speechKey, speechRegion);
            speechTranslationConfig.SpeechRecognitionLanguage = speechRecognitionLanguage;
            speechTranslationConfig.AddTargetLanguage("en-US");

            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            using var translationRecognizer = new TranslationRecognizer(speechTranslationConfig, audioConfig);

            Console.WriteLine("Speak into your microphone.");
            var translationRecognitionResult = await translationRecognizer.RecognizeOnceAsync();

            switch (translationRecognitionResult.Reason)
            {
                case ResultReason.TranslatedSpeech:
                    Console.WriteLine($"RECOGNIZED: Text={translationRecognitionResult.Text}");
                    var translation = translationRecognitionResult.Translations.First();
                    Console.WriteLine($"TRANSLATED into '{translation.Key}': {translation.Value}");
                    return (translationRecognitionResult.Text, translation.Value);
                case ResultReason.NoMatch:
                    Console.WriteLine("NOMATCH: Speech could not be recognized.");
                    return new Error();
                case ResultReason.Canceled:
                    var cancellation = CancellationDetails.FromResult(translationRecognitionResult);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Console.WriteLine("CANCELED: Did you set the speech resource key and region values?");
                    }

                    return new Error();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        async Task<string> OutputPredictionAsync()
        {
            var data = new
            {
                AnalysisInput = new
                {
                    ConversationItem = new
                    {
                        Text = translated,
                        Id = "1",
                        ParticipantId = "1",
                    }
                },
                Parameters = new
                {
                    ProjectName = cluProjectName,
                    DeploymentName = cluDeploymentName,
                    StringIndexType = "Utf16CodeUnit",
                },
                Kind = "Conversation",
            };

            var endpoint = new Uri(languageEndpoint);
            var credential = new AzureKeyCredential(languageKey);
            var client = new ConversationAnalysisClient(endpoint, credential);
            var response =
                await client.AnalyzeConversationAsync(RequestContent.Create(data, JsonPropertyNames.CamelCase));

            var conversationalTaskResult = response.Content.ToDynamicFromJson(JsonPropertyNames.CamelCase);
            var conversationPrediction = conversationalTaskResult.Result.Prediction;

            Console.WriteLine($"Top intent: {conversationPrediction.TopIntent}");

            Console.WriteLine("Intents:");
            foreach (var intent in conversationPrediction.Intents)
            {
                Console.WriteLine($"Category: {intent.Category}");
                Console.WriteLine($"Confidence: {intent.ConfidenceScore}");
                Console.WriteLine();
            }

            Console.WriteLine("Entities:");
            foreach (var entity in conversationPrediction.Entities)
            {
                Console.WriteLine($"Category: {entity.Category}");
                Console.WriteLine($"Text: {entity.Text}");
                Console.WriteLine($"Offset: {entity.Offset}");
                Console.WriteLine($"Length: {entity.Length}");
                Console.WriteLine($"Confidence: {entity.ConfidenceScore}");
                Console.WriteLine();

                if (entity.Resolutions is not null)
                {
                    foreach (var resolution in entity.Resolutions)
                    {
                        if (resolution.ResolutionKind == "DateTimeResolution")
                        {
                            Console.WriteLine($"Datetime Sub Kind: {resolution.DateTimeSubKind}");
                            Console.WriteLine($"Timex: {resolution.Timex}");
                            Console.WriteLine($"Value: {resolution.Value}");
                            Console.WriteLine();
                        }
                    }
                }
            }

            isRecording = false;
            return conversationPrediction.TopIntent;
        }
    }

    private void NavigateBasedOnIntent(string intent)
    {
        switch (intent)
        {
            case "Appointment Booking":
                NavigationManager.NavigateTo("/booking");
                break;
            case "Medicine Refill":
                NavigationManager.NavigateTo("/checkout");
                break;
            default:
                NavigationManager.NavigateTo("/");
                break;
        }

        return;
    }
}