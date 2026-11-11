using SistemaGestaoInfra.Domain.Common;

namespace SistemaGestaoInfra.Domain.Entities;

/// <summary>Empresa contratada pela prefeitura.</summary>
public class Fornecedor : EntidadeTenant
{
    public string RazaoSocial { get; set; } = string.Empty;

    /// <summary>CNPJ somente com dígitos (14), único por prefeitura.</summary>
    public string Cnpj { get; set; } = string.Empty;

    public string? Contato { get; set; }
    public bool Ativo { get; set; } = true;
}

/// <summary>Unidade de medição de um serviço (ex.: M2, HORA, UN, KM).</summary>
public class TipoMedicao : EntidadeTenant, ICadastroSimples
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

/// <summary>Serviço que pode constar em contratos (ex.: capina manual, tapa-buraco).</summary>
public class TipoServico : EntidadeTenant, ICadastroSimples
{
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}
