using Hl7.Fhir.Model;
using Microsoft.Extensions.Hosting;
using Task = System.Threading.Tasks.Task;

namespace IKJ.Client;

public class Application : IHostedService
{
    private readonly IIkjApi _ikjApi;
    private readonly InputFhirJsonReader _inputFhirJsonReader;

    public Application(IIkjApi ikjApi, InputFhirJsonReader inputFhirJsonReader)
    {
        _ikjApi = ikjApi;
        _inputFhirJsonReader = inputFhirJsonReader;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Application started at {DateTimeOffset.Now}");

        /*
         * Reads patients from local file system, and makes sure that they exist
         * in IKJ for the current organization. If patients do not exist already
         * they are added using POST.
         */
        Console.WriteLine("READ INPUT PATIENTS{0}----------------------", Environment.NewLine);

        var patients = _inputFhirJsonReader.GetInputPatients();
        if (patients.Count == 0)
        {
            await Console.Error.WriteLineAsync("No input patients found - nothing to do!");
        }

        foreach (var patient in patients)
        {
            await EnsurePatientExists(patient, cancellationToken);
        }

        /*
         * Updates first Patient resource based on the input resource, with a
         * random identifier added.
         */
        Console.WriteLine("{0}UPDATE PATIENT{0}-----------------", Environment.NewLine);

        var thePatient = patients.First();
        await UpdatePatient(thePatient, cancellationToken);
        await ReadPatientById(thePatient.Id!, CancellationToken.None);

        /*
         * EnsurePatientHasSomeEpisodesOfCare.
         */
        // TBD


        // Then: Read EpisodesOfCare and add to a patient - the first one.
        // Then: Get EpisodeOfCare with paging and include patient.


        Console.WriteLine("Done!");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Searches for the patient using the API. If the patient was not found
    ///     it is added. Either way the input patient's ID is set based on the
    ///     API response.
    /// </summary>
    /// <param name="patient"></param>
    /// <param name="ct"></param>
    private async Task EnsurePatientExists(Patient patient, CancellationToken ct)
    {
        var identifier = patient.Identifier.First();
        var system = identifier.System ?? string.Empty;
        var value = identifier.Value ?? string.Empty;

        var retrievedPatient = await _ikjApi.GetPatientByIdentifierAsync(system, value, ct);
        if (retrievedPatient is not null)
        {
            Console.WriteLine(
                "Skipping POST - Patient '{0}|{1}' already exists: id={2}",
                system,
                value,
                retrievedPatient.Id);

            patient.Id = retrievedPatient.Id;
            return;
        }

        Console.WriteLine($"Patient '{system}|{value}' not found. Adding it now!");

        var (taskUrl, pollIntervalS) = await _ikjApi.PostPatientAsync(patient, ct);
        var createdPatientId = await _ikjApi.PollTaskStatusAsync(taskUrl, pollIntervalS, ct);
        patient.Id = createdPatientId;

        Console.WriteLine($"Patient '{system}|{value}' created successfully: id={createdPatientId}");
    }

    /// <summary>
    ///     Updates the patient using the API. The passed-in resource is added 'as-is',
    ///     with the exception that a new, random identifier is added.
    /// </summary>
    /// <param name="patient"></param>
    /// <param name="ct"></param>
    private async Task UpdatePatient(Patient patient, CancellationToken ct)
    {
        const string system = "https://komsat.samples.example.com/ikj";
        var value = Guid.NewGuid().ToString();

        patient.Identifier.Add(
            new Identifier
            {
                System = system,
                Value = value
            });

        Console.WriteLine(
            "Updating patient: id={0}, newIdent={1}|{2} ...",
            patient.Id,
            system,
            value);

        var (taskUrl, pollIntervalS) = await _ikjApi.PutPatientAsync(patient, ct);
        var updatedPatientId = await _ikjApi.PollTaskStatusAsync(taskUrl, pollIntervalS, ct);

        Console.WriteLine($"Patient updated successfully: id={updatedPatientId}");
    }

    /// <summary>
    ///     Reads the patient from the API by the patient's ID.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    private async Task ReadPatientById(string id, CancellationToken ct)
    {
        var retrievedPatient = await _ikjApi.GetPatientByIdAsync(id, ct);
        var identifiers = retrievedPatient.Identifier;
        Console.WriteLine(
            "Read patient from IKJ: id={0}, numOfIdentifiers={1}",
            retrievedPatient.Id,
            identifiers.Count);

        foreach (var identifier in retrievedPatient.Identifier)
        {
            Console.WriteLine($"\tRead patient identifier: '{identifier.System}|{identifier.Value}':");
        }
    }
}
