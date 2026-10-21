using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace IKJ.Client;

public class InputFhirJsonReader
{
    private readonly FhirJsonDeserializer _deserializer;

    public InputFhirJsonReader(FhirJsonDeserializer deserializer)
    {
        _deserializer = deserializer;
    }

    /// <summary>
    ///     Reads JSON files in "Patients" folder and deserializes them into FHIR Patient resources.
    /// </summary>
    /// <returns></returns>
    public IList<Patient> GetInputPatients()
    {
        var patients = GetInputResources<Patient>("Patients");

        foreach (var patient in patients)
        {
            var identifier = patient.Identifier.First();
            Console.WriteLine("Found patient {0}: {1}", patient.GetName(), identifier.AsString());
        }

        return patients;
    }

    /// <summary>
    ///     Reads JSON files in "EpisodesOfCare" folder and deserializes them into FHIR EpisodeOfCare resources.
    /// </summary>
    /// <returns></returns>
    public IList<EpisodeOfCare> GetInputEpisodesOfCare()
    {
        var episodesOfCare = GetInputResources<EpisodeOfCare>("EpisodesOfCare");

        foreach (var episodeOfCare in episodesOfCare)
        {
            var identifier = episodeOfCare.Identifier.FirstOrDefault();
            Console.WriteLine("Found episode of care: {0}", identifier?.AsString() ?? "<no identifier>");
        }

        return episodesOfCare;
    }

    /// <summary>
    ///     Loops over files in the specified folder, and tries to deserialize the data as the FHIR resource
    ///     specified by the template type parameter. The deserializer uses a Poco-R4-validator, so an exception
    ///     is thrown if the input resource is not valid R4.
    /// </summary>
    /// <param name="folder"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private IList<T> GetInputResources<T>(string folder) where T : Resource
    {
        var jsonFiles = Directory.GetFiles(folder, "*.json");

        return
        [
            .. jsonFiles.Select(File.ReadAllText).Select(resourceText => _deserializer.Deserialize<T>(resourceText))
        ];
    }
}
