namespace KiCadDoxer.Renderer.Schematic
{
    internal enum Shape
    {
        [EnumStringValue("UnSpc")]

        // "Passive" in the UI
        Unspecified,

        [EnumStringValue("Input")]
        Input,

        [EnumStringValue("Output")]
        Output,

        // Renders identical, so I just chose Bidirectional for now.
        [EnumStringValue("BiDi", "3State")]
        Bidirectional,
    }
}