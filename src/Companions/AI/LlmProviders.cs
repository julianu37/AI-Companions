using System;

namespace Companions;

/// <summary>
/// Fase 3.21 - o <b>provedor do LLM</b> como atalho de preenchimento: o dropdown que o painel I mostra na secao do LLM
/// ("Selecione seu Provedor LLM") com os provedores que o mod documenta, cada um sabendo a URL e o modelo que usa.
///
/// <para>
/// A escolha do provedor NAO e uma config nova, de proposito: o <c>[IA] BaseUrl</c> continua sendo a fonte da verdade
/// (e o unico dado que o <see cref="LlmClient"/> conhece), e um segundo lugar dizendo "qual e o servidor" passaria a
/// mentir no instante em que o jogador editasse o <c>.cfg</c> na mao - ou trocasse de servidor por fora do painel.
/// Por isso o item mostrado pelo dropdown e <b>derivado</b> da URL atual (<see cref="Suggest"/>): ele e um atalho que
/// copia a URL e o modelo para os campos de texto, nada mais. Quem grava no <c>.cfg</c> continua sendo o "Aplicar" da
/// secao (os campos de texto so valem depois dele) e a ApiKey nunca e tocada aqui.
/// </para>
///
/// <para>
/// Pura de proposito (sem Unity, sem config, sem mundo): a tabela e o casamento de URL podem ser conferidos fora do
/// jogo, e o painel so consome <see cref="Count"/>, <see cref="LabelKeyAt"/>, <see cref="BaseUrlAt"/>,
/// <see cref="ModelAt"/>, <see cref="IdAt"/>, <see cref="Fills"/> e <see cref="Suggest"/>.
/// </para>
/// </summary>
internal static class LlmProviders
{
	/// <summary>
	/// Um provedor da tabela: o nome que sai no log (<see cref="Id"/>), a chave de localizacao do rotulo do dropdown
	/// (<see cref="LabelKey"/> - o texto em si vem do <see cref="ModLocalization"/>, para esta classe seguir sem
	/// dependencia de UI), o que ele preenche (<see cref="BaseUrl"/> e <see cref="Model"/>) e a pagina onde o jogador
	/// cria a chave dele (<see cref="KeyUrl"/>).
	/// </summary>
	internal struct LlmProvider
	{
		internal string Id;

		internal string LabelKey;

		internal string BaseUrl;

		internal string Model;

		/// <summary>Outra URL aceita como o MESMO provedor, ou "". O LM Studio escuta em <c>127.0.0.1</c> ou em
		/// <c>localhost</c> e as duas formas sao o mesmo servidor - em qualquer outro caso elas seriam servidores
		/// diferentes, entao a equivalencia mora aqui e nao no casamento de URL (ver <see cref="Key"/>).</summary>
		internal string Alias;

		/// <summary>
		/// A pagina de chaves do provedor, aberta no navegador pelo link "Crie sua Chave de API Grátis AQUI" do painel
		/// - ou "" quando nao ha pagina (o LM Studio roda na maquina do jogador e nao pede chave, e o "Personalizado"
		/// pode ser qualquer servidor). E uma <b>constante desta tabela</b>, escrita aqui e em nenhum outro lugar: o
		/// link nunca monta a URL com nada que o jogador tenha digitado, que e a regra de seguranca do
		/// <c>Application.OpenURL</c> (so abre o que o mod conhece). Por isso o link so aparece nos provedores que tem
		/// uma - ver <see cref="KeyUrlAt"/>.
		/// </summary>
		internal string KeyUrl;
	}

	/// <summary>
	/// A tabela, na ordem em que o dropdown mostra. O primeiro item e o default do <c>.cfg</c> desde a 1.8.1
	/// (Groq), entao quem nunca mexeu no arquivo abre o dropdown ja nele; <c>Alias</c> so o LM Studio tem, e o
	/// ultimo item e o "Personalizado", que nao preenche nada.
	/// </summary>
	private static readonly LlmProvider[] Table = new LlmProvider[]
	{
		new LlmProvider
		{
			Id = "Groq",
			LabelKey = "hc_ai_llm_provider_groq",
			BaseUrl = "https://api.groq.com/openai/v1",
			Model = "openai/gpt-oss-20b",
			KeyUrl = "https://console.groq.com/keys"
		},
		new LlmProvider
		{
			Id = "LM Studio",
			LabelKey = "hc_ai_llm_provider_lmstudio",
			BaseUrl = "http://127.0.0.1:1234/v1",
			Model = "qwen2.5-3b-instruct",
			Alias = "http://localhost:1234/v1"
			// sem KeyUrl: o LM Studio roda na maquina do jogador e nao tem chave para criar
		},
		new LlmProvider
		{
			Id = "OpenRouter",
			LabelKey = "hc_ai_llm_provider_openrouter",
			BaseUrl = "https://openrouter.ai/api/v1",
			Model = "openrouter/free",
			KeyUrl = "https://openrouter.ai/keys"
		},
		// nao preenche nada: existe para o dropdown dizer a verdade quando a URL do .cfg nao e de nenhum dos tres
		// (Ollama, um servidor proprio...) - e para o jogador poder deixar os campos como estao. Sem KeyUrl: nao ha
		// pagina de chaves para um servidor que o mod nao conhece.
		new LlmProvider
		{
			Id = "personalizado",
			LabelKey = "hc_ai_llm_provider_custom",
			BaseUrl = "",
			Model = ""
		}
	};

	/// <summary>Quantos itens o dropdown tem (tres provedores + "Personalizado").</summary>
	internal static int Count => Table.Length;

	/// <summary>O indice do "Personalizado (URL manual)" - o unico item que nao preenche nada.</summary>
	internal static int CustomIndex => Table.Length - 1;

	internal static LlmProvider At(int index)
	{
		if (index < 0 || index >= Table.Length)
		{
			return Table[Table.Length - 1];
		}
		return Table[index];
	}

	internal static string IdAt(int index)
	{
		return At(index).Id ?? "";
	}

	internal static string LabelKeyAt(int index)
	{
		return At(index).LabelKey ?? "";
	}

	internal static string BaseUrlAt(int index)
	{
		return At(index).BaseUrl ?? "";
	}

	internal static string ModelAt(int index)
	{
		return At(index).Model ?? "";
	}

	/// <summary>A pagina de chaves do provedor (constante da tabela), ou "" quando ele nao tem uma - o painel usa isso
	/// para decidir se mostra o link "Crie sua Chave de API Grátis AQUI".</summary>
	internal static string KeyUrlAt(int index)
	{
		return At(index).KeyUrl ?? "";
	}

	/// <summary>O item preenche a URL e o modelo? (falso so no "Personalizado")</summary>
	internal static bool Fills(int index)
	{
		return BaseUrlAt(index).Length > 0 && ModelAt(index).Length > 0;
	}

	/// <summary>
	/// O item do dropdown que corresponde a URL atual: o indice do provedor dono dela, ou o do "Personalizado"
	/// quando nenhum casa (inclusive URL vazia). A comparacao e tolerante de proposito - o jogador cola a URL com
	/// barra no fim, com <c>/chat/completions</c> (que o cliente acrescenta sozinho) ou sem o <c>/v1</c>.
	/// </summary>
	internal static int Suggest(string baseUrl)
	{
		string key = Key(baseUrl);
		if (key.Length == 0)
		{
			return CustomIndex;
		}
		for (int i = 0; i < Table.Length; i++)
		{
			if (Key(Table[i].BaseUrl) == key)
			{
				return i;
			}
			if (Key(Table[i].Alias) == key)
			{
				return i;
			}
		}
		return CustomIndex;
	}

	/// <summary>
	/// A forma comparavel de uma URL: sem espacos nas pontas, em minusculas, <b>sem o esquema</b> (http/https - o
	/// mesmo servidor nas duas), sem o <c>/chat/completions</c> que o <see cref="LlmClient"/> acrescenta sozinho, sem
	/// barra no fim e sem o <c>/v1</c> (com e sem ele e o mesmo servidor para o cliente). <c>localhost</c> e
	/// <c>127.0.0.1</c> NAO viram a mesma coisa aqui: isso e uma equivalencia do LM Studio, e vive no
	/// <see cref="LlmProvider.Alias"/>.
	/// </summary>
	private static string Key(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return "";
		}
		string key = url.Trim().ToLowerInvariant();
		if (key.StartsWith("http://", StringComparison.Ordinal))
		{
			key = key.Substring("http://".Length);
		}
		else if (key.StartsWith("https://", StringComparison.Ordinal))
		{
			key = key.Substring("https://".Length);
		}
		// the trailing slash goes first: "…/v1/chat/completions/" is the same server as "…/v1"
		key = key.TrimEnd('/');
		key = TrimSuffix(key, "/chat/completions");
		key = TrimSuffix(key, "/v1");
		return key.TrimEnd('/');
	}

	private static string TrimSuffix(string value, string suffix)
	{
		if (value.Length >= suffix.Length && value.EndsWith(suffix, StringComparison.Ordinal))
		{
			return value.Substring(0, value.Length - suffix.Length);
		}
		return value;
	}
}
