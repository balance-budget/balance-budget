using Balance.Integration.Ing.Models.Notes;

namespace Balance.Integration.Ing.Contracts;

internal interface IIngNoteParser
{
    public IngNote ParseNote(string note);
}
