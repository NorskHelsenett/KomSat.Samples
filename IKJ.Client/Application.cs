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
        /*
         * Reads patients from local file system, and makes sure that they exist
         * in IKJ for the current organization. If patients do not exist already
         * they are added using POST.
         */
        Console.WriteLine("ADD INPUT PATIENTS{0}----------------------", Environment.NewLine);

        var patients = _inputFhirJsonReader.GetInputPatients();
        if (patients.Count == 0)
        {
            await Console.Error.WriteLineAsync("No input patients found - nothing to do!");
            return;
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
         * Reads episodes of care from local file system, and makes sure that they exist
         * in IKJ for the current organization and patient. If episodes of care do not exist
         * already, then they are added using POST.
         */
        Console.WriteLine("{0}ADD INPUT EPISODES-OF-CARE{0}-----------------------------", Environment.NewLine);

        var inputEpisodesOfCare = _inputFhirJsonReader.GetInputEpisodesOfCare();
        if (inputEpisodesOfCare.Any())
        {
            foreach (var episodeOfCare in inputEpisodesOfCare)
            {
                await EnsureEpisodesOfCareExists(episodeOfCare, thePatient, cancellationToken);
            }
        }
        else
        {
            Console.WriteLine("No input episodes of care found ...");
        }

        /*
         * Misc EpisodeOfCare searches.
         */
        Console.WriteLine("{0}EPISODES-OF-CARE FOR PATIENT{0}-------------------------------", Environment.NewLine);
        await SearchForEpisodesOfCareForPatientAsync(thePatient, cancellationToken);


        Console.WriteLine(
            "{0}EPISODES-OF-CARE WITH KPR/IPLOS CODING{0}-----------------------------------------",
            Environment.NewLine);

        var kprCodeSystem = new Coding { System = Configuration.KprCodeSystem };
        var managingOrg = new Identifier { System = Configuration.AreaCodeSystem, Value = "1.2" };
        await SearchForEpisodesOfCareByTypeAsync(kprCodeSystem, managingOrg, cancellationToken);

        kprCodeSystem.Code = "6";
        await SearchForEpisodesOfCareByTypeAsync(kprCodeSystem, null, cancellationToken);

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

        var retrievedPatient = await _ikjApi.GetResourceByIdentifierAsync<Patient>(system, value, ct);
        if (retrievedPatient is not null)
        {
            Console.WriteLine(
                "Skipping POST - Patient '{0}' already exists: name={1}",
                identifier.AsString(),
                retrievedPatient.GetName());

            patient.Id = retrievedPatient.Id;
            return;
        }

        Console.WriteLine("Patient '{0}' not found. Adding {1} now!", identifier.AsString(), patient.GetName());

        var (taskUrl, pollIntervalS) = await _ikjApi.PostResourceAsync(patient, ct);
        var createdPatientId = await _ikjApi.PollTaskStatusAsync(taskUrl, pollIntervalS, ct);
        patient.Id = createdPatientId;

        Console.WriteLine("Patient '{0}' created successfully: id={1}", patient.GetName(), createdPatientId);
    }

    /// <summary>
    ///     Updates the patient using the API. The passed-in resource is added 'as-is',
    ///     with the exception that a new, random identifier is added.
    /// </summary>
    /// <param name="patient"></param>
    /// <param name="ct"></param>
    private async Task UpdatePatient(Patient patient, CancellationToken ct)
    {
        const string system = "https://komsat.samples.example.com/ikj/id";
        var value = Guid.NewGuid().ToString();

        var identifier = new Identifier
        {
            System = system,
            Value = value
        };
        patient.Identifier.Add(identifier);

        Console.WriteLine("Updating patient: id={0}, newIdent={1} ...", patient.Id, identifier.AsString());

        var (taskUrl, pollIntervalS) = await _ikjApi.PutResourceAsync(patient, ct);
        var updatedPatientId = await _ikjApi.PollTaskStatusAsync(taskUrl, pollIntervalS, ct);

        Console.WriteLine("Patient {0} updated successfully: id={1}", patient.GetName(), updatedPatientId);
    }

    /// <summary>
    ///     Reads the patient from the API by the patient's ID.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct"></param>
    private async Task ReadPatientById(string id, CancellationToken ct)
    {
        var retrievedPatient = await _ikjApi.GetResourceByIdAsync<Patient>(id, ct);
        var identifiers = retrievedPatient.Identifier;
        Console.WriteLine(
            "Read patient {0} from IKJ: id={1}, numOfIdentifiers={2}",
            retrievedPatient.GetName(),
            retrievedPatient.Id,
            identifiers.Count);

        foreach (var identifier in retrievedPatient.Identifier)
        {
            Console.WriteLine($"\tRead patient identifier: '{identifier.AsString()}'");
        }
    }

    /// <summary>
    ///     Searches for all EpisodeOfCare resources for a given patient. A limited number of pages are processed.
    /// </summary>
    /// <param name="patient"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task SearchForEpisodesOfCareForPatientAsync(Patient patient, CancellationToken ct)
    {
        Console.WriteLine("Searching for episodes of care for patient {0}: id={1}", patient.GetName(), patient.Id);

        var pages = 0;
        var itemNo = 0;
        string? nextPageCursor = null;

        do
        {
            ++pages;
            var (episodesOfCare, cursor) =
                await _ikjApi.GetEpisodesOfCareForPatientAsync(patient.Id!, nextPageCursor, ct);

            Console.WriteLine("Retrieved {0} item(s) on page {1}:", episodesOfCare.Count, pages);

            foreach (var episodeOfCare in episodesOfCare)
            {
                ++itemNo;
                Console.WriteLine(
                    "\tEoC {0}: systemCode={1}, patient={2}",
                    itemNo,
                    episodeOfCare.Type.Single().Coding.First().AsString(),
                    episodeOfCare.Patient.Reference?.Split('/').Last());
            }

            nextPageCursor = cursor;
        } while (nextPageCursor != null && pages < Configuration.PageMaxCount);

        if (nextPageCursor != null)
        {
            Console.WriteLine("\t(Ignoring following pages)");
        }

        Console.WriteLine();
    }

    /// <summary>
    ///     Adds a new EpisodeOfCare using POST, as long as it doesn't exist already.
    /// </summary>
    /// <param name="episodeOfCare">EpisodeOfCare to be added</param>
    /// <param name="patient">Patient the EpisodeOfCare refers to</param>
    /// <param name="ct"></param>
    private async Task EnsureEpisodesOfCareExists(
        EpisodeOfCare episodeOfCare,
        Patient patient,
        CancellationToken ct)
    {
        var identifier = episodeOfCare.Identifier.FirstOrDefault();

        if (identifier is not null)
        {
            var system = identifier.System ?? string.Empty;
            var value = identifier.Value ?? string.Empty;
            var retrieved = await _ikjApi.GetResourceByIdentifierAsync<EpisodeOfCare>(system, value, ct);

            if (retrieved is not null)
            {
                var retrievedPatientReference = retrieved.Patient.Reference!.Split('/').Last();

                Console.WriteLine(
                    "Skipping POST - EpisodeOfCare '{0}' already exists{1}: id={2}, patient={3}",
                    identifier.AsString(),
                    retrievedPatientReference != patient.Id
                        ? " (for another patient)"
                        : "",
                    retrieved.Id,
                    retrievedPatientReference);

                episodeOfCare.Id = retrieved.Id;
                return;
            }

            Console.WriteLine(
                "EpisodeOfCare '{0}' not found. Adding it to patient {1} now!",
                identifier.AsString(),
                patient.GetName());
        }

        episodeOfCare.Patient.Reference = $"Patient/{patient.Id}";

        var (taskUrl, pollIntervalS) = await _ikjApi.PostResourceAsync(episodeOfCare, ct);
        var createdEpisodeOfCareId = await _ikjApi.PollTaskStatusAsync(taskUrl, pollIntervalS, ct);
        episodeOfCare.Id = createdEpisodeOfCareId;

        Console.WriteLine(
            "EpisodeOfCare '{0}' created successfully for patient {1}: id={2}",
            identifier?.AsString() ?? "<no identifiers>",
            patient.GetName(),
            createdEpisodeOfCareId);
    }

    /// <summary>
    ///     Searches for EpisodeOfCare resources based on coding. Only first page is processed.
    /// </summary>
    /// <param name="type">Coding filter</param>
    /// <param name="managingOrganization">Area filter</param>
    /// <param name="ct"></param>
    private async Task SearchForEpisodesOfCareByTypeAsync(
        Coding type,
        Identifier? managingOrganization,
        CancellationToken ct)
    {
        Console.WriteLine(
            "Searching for episodes of care by type: codeFilter={0}, managingOrgFilter={1}",
            type.AsString(),
            managingOrganization?.AsString() ?? "<not specified>");

        var (episodesOfCare, nextPageCursor) =
            await _ikjApi.GetEpisodesOfCareByTypeAsync(
                type,
                managingOrganization,
                null,
                ct);

        Console.WriteLine("Retrieved {0} item(s) on first page:", episodesOfCare.Count);
        var itemNo = 0;

        foreach (var episodeOfCare in episodesOfCare)
        {
            ++itemNo;
            Console.WriteLine(
                "\tEoC {0}: systemCode={1}, managingOrg={2}",
                itemNo,
                episodeOfCare.Type.Single().Coding.First(c => c.System == type.System).AsString(),
                episodeOfCare.ManagingOrganization?.Identifier?.AsString() ?? "<no value>");
        }

        if (nextPageCursor != null)
        {
            Console.WriteLine("\t(Ignoring following pages)");
        }

        Console.WriteLine();
    }
}
