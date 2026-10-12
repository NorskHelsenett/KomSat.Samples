using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace IKJ.Client;

public interface IIkjApi
{
    /// <summary>
    ///     Reads Patient from IKJ by ID.
    /// </summary>
    /// <param name="id">Resource ID</param>
    /// <param name="ct"></param>
    /// <returns>Patient</returns>
    Task<Patient> GetPatientByIdAsync(string id, CancellationToken ct);

    /// <summary>
    ///     Gets Patient from IKJ by identifier.
    /// </summary>
    /// <param name="system">Identifier system</param>
    /// <param name="value">Identifier value</param>
    /// <param name="ct"></param>
    /// <returns>Patient if found, or null</returns>
    Task<Patient?> GetPatientByIdentifierAsync(
        string system,
        string value,
        CancellationToken ct);

    /// <summary>
    ///     Sends a request to create a Patient in IKJ. The create operation is asynchronous.
    ///     If the request is valid, a Task tracking the create operation progress is returned.
    /// </summary>
    /// <param name="patient">Patient to be created</param>
    /// <param name="ct"></param>
    /// <returns>Task and retry-after value</returns>
    Task<(string, int)> PostPatientAsync(Patient patient, CancellationToken ct);

    /// <summary>
    ///     Sends a request to update (replace) a Patient in IKJ. The update operation is asynchronous.
    ///     If the request is valid, a Task tracking the update operation progress is returned.
    /// </summary>
    /// <param name="patient">Patient to be updated</param>
    /// <param name="ct"></param>
    /// <returns>Task and retry-after value</returns>
    Task<(string, int)> PutPatientAsync(Patient patient, CancellationToken ct);

    /// <summary>
    ///     Polls IKJ task until it is 'completed'.
    /// </summary>
    /// <param name="url">Task URL, including the unique task ID</param>
    /// <param name="pollIntervalS">Time between poll requests</param>
    /// <param name="ct"></param>
    /// <returns>Patient ID</returns>
    /// <exception cref="Exception"></exception>
    Task<string> PollTaskStatusAsync(
        string url,
        int pollIntervalS,
        CancellationToken ct);
}

public class IkjApi : IIkjApi
{
    private const string IkjBaseUrl = "https://api.ikj.test.nhn.no/v1";
    private const int PollTaskStatusMaxAttempts = 5;

    private readonly IkjClient _client;
    private readonly FhirJsonDeserializer _fhirDeserializer;
    private readonly FhirJsonSerializer _fhirSerializer;

    public IkjApi(
        IkjClient client,
        FhirJsonDeserializer deserializer,
        FhirJsonSerializer serializer)
    {
        _client = client;
        _fhirDeserializer = deserializer;
        _fhirSerializer = serializer;
    }

    public async Task<Patient> GetPatientByIdAsync(string id, CancellationToken ct)
    {
        var response = await _client.SendAsync(
            HttpMethod.Get,
            $"{IkjBaseUrl}/Patient/{id}",
            null,
            null,
            ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        return _fhirDeserializer.Deserialize<Patient>(json);
    }

    public async Task<Patient?> GetPatientByIdentifierAsync(
        string system,
        string value,
        CancellationToken ct)
    {
        var response = await _client.SendAsync(
            HttpMethod.Get,
            $"{IkjBaseUrl}/Patient",
            $"identifier={system}|{value}",
            null,
            ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        // Search using querystring returns a Bundle, but we expect a single Patient response as we search by identifier.
        var bundle = _fhirDeserializer.Deserialize<Bundle>(json);
        var entryComponent = bundle.Entry.SingleOrDefault();
        return entryComponent?.Resource as Patient;
    }

    public async Task<(string, int)> PostPatientAsync(Patient patient, CancellationToken ct)
    {
        var patientJson = _fhirSerializer.SerializeToString(patient);
        var response = await _client.SendAsync(
            HttpMethod.Post,
            $"{IkjBaseUrl}/Patient",
            null,
            patientJson,
            ct);

        // Successful posts results in a Task being created in IKJ. The Task status must be polled to check
        // for the status of the asynchronous operation.
        return GetTaskInformation(response);
    }

    public async Task<(string, int)> PutPatientAsync(Patient patient, CancellationToken ct)
    {
        var patientJson = _fhirSerializer.SerializeToString(patient);
        var response = await _client.SendAsync(
            HttpMethod.Put,
            $"{IkjBaseUrl}/Patient/{patient.Id}",
            null,
            patientJson,
            ct);

        // Successful posts results in a Task being created in IKJ. The Task status must be polled to check
        // for the status of the asynchronous operation.
        return GetTaskInformation(response);
    }

    public async Task<string> PollTaskStatusAsync(
        string url,
        int pollIntervalS,
        CancellationToken ct)
    {
        var attemptCount = 0;
        await System.Threading.Tasks.Task.Delay(500, ct);

        while (attemptCount < PollTaskStatusMaxAttempts)
        {
            ++attemptCount;
            var response = await _client.SendAsync(
                HttpMethod.Get,
                url,
                null,
                null,
                ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var task = _fhirDeserializer.Deserialize<Hl7.Fhir.Model.Task>(json);

            Console.WriteLine(
                "Polling task status: status={0}, attempt={1}, maxAttempts={2}",
                task.Status,
                attemptCount,
                PollTaskStatusMaxAttempts);

            if (task.Status == Hl7.Fhir.Model.Task.TaskStatus.Failed)
            {
                throw new Exception(
                    $"Task status indicated that operation failed: status={task.Status}, code={task.Code?.Text}, description={task.Description}");
            }

            if (task.Status != Hl7.Fhir.Model.Task.TaskStatus.Completed)
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(pollIntervalS), ct);
                continue;
            }

            var outputComponent = task.Output.Single();
            var resourceReference = (ResourceReference)outputComponent.Value;
            return resourceReference.Reference!.Split('/').Last();
        }

        throw new Exception(
            "Task polling aborted due to max attempts limit being reached, and Task is still not completed.");
    }

    private static (string, int) GetTaskInformation(HttpResponseMessage response)
    {
        var taskUrl = response
            .Headers
            .Single(h => h.Key.Equals("location", StringComparison.InvariantCultureIgnoreCase))
            .Value
            .Single();

        var retryAfterS = Convert.ToInt32(
            response
                .Headers
                .Single(h => h.Key.Equals("retry-after", StringComparison.InvariantCultureIgnoreCase))
                .Value
                .Single());

        return (taskUrl, retryAfterS);
    }
}
