using Balance.Integration.Pdf;

namespace Balance.Integration.Ing.Helpers;

// One credit-card statement file, offered to every layout in the form that layout needs: the PDF
// layouts read reconstructed text lines, the CSV layout reads the stream. Both the sniffing probe
// and the parse that follows go through the same instance, so a PDF is turned into text once per
// file no matter how many layouts inspect it.
//
// The caller owns the stream — detection probes a file and then re-reads it for the actual import
// (ADR 0034) — so this type rewinds but never disposes it.
internal sealed class IngCreditCardStatementSource
{
    private readonly Stream _stream;
    private List<string>? _lines;

    public IngCreditCardStatementSource(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        LooksLikePdf = PdfTextReader.LooksLikePdf(stream);
    }

    public bool LooksLikePdf { get; }

    // The rewound stream, for layouts that read the bytes themselves.
    public Stream Stream
    {
        get
        {
            Rewind();
            return _stream;
        }
    }

    // Reconstructed PDF text lines, extracted once and reused. Empty for a non-PDF file, which is
    // how the PDF layouts decline a CSV drop without the PDF reader ever seeing it.
    public IReadOnlyList<string> GetLines(CancellationToken cancellationToken)
    {
        if (_lines is not null)
            return _lines;

        if (!LooksLikePdf)
        {
            _lines = [];
            return _lines;
        }

        Rewind();
        _lines =
        [
            .. PdfTextReader.ExtractLines(_stream, cancellationToken).Select(line => line.Text),
        ];
        return _lines;
    }

    private void Rewind()
    {
        if (_stream.CanSeek)
            _stream.Seek(0, SeekOrigin.Begin);
    }
}
