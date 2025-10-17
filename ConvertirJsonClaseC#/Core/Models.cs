namespace ConvertirJsonClaseC_.Core
{
    /// <summary>
    /// Propósito: Describa el propósito para esta clase.
    /// Fecha de creación: 16/10/2025 10:16:47.
    /// Creador: CAPOME (banr25734).
    /// Modificó:
    /// Dependencias de conexiones e interfaces: No Aplica.
    /// </summary>
    public class ClassDef
    {
        public string ClassName { get; set; } = "";
        public string? ClassSummary { get; set; }
        public List<PropertyDef> Properties { get; set; } = new();
    }

    public class PropertyDef
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Summary { get; set; }
    }
}
