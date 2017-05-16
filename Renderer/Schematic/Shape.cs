namespace KiCadDoxer.Renderer.Schematic
{
    internal enum Shape
    {
        [EnumStringValue("UnSpc", "U")]

        // "Passive" in the UI
        Unspecified,

        [EnumStringValue("Input", "I")]
        Input,

        [EnumStringValue("Output", "O")]
        Output,

        // Renders identical, so I just chose Bidirectional for now.
        [EnumStringValue("BiDi", "3State", "B", "T")]
        Bidirectional,
    }
}