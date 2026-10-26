using Microsoft.Playwright;
using Xunit;

namespace WalletFlow.BrowserTests;

[Collection(WalletFlowBrowserCollection.Name)]
public sealed class IdentityPagesBrowserTests(WalletFlowBrowserFixture fixture)
{
    [Theory]
    [InlineData("/", "Acesse sua conta", ".login-card")]
    [InlineData("/esqueci-a-senha", "Esqueceu sua senha?", ".identity-card")]
    [InlineData("/criar-conta", "Criar sua conta", ".identity-card")]
    public async Task PaginasCarregamComEstiloResponsivo(string route, string heading, string cardSelector)
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), route.TrimStart('/')).AbsoluteUri);

        await page.GetByRole(AriaRole.Heading, new() { Name = heading }).WaitForAsync();
        var card = page.Locator(cardSelector);
        Assert.True(await card.IsVisibleAsync());

        var cardBackground = await card.EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");
        var pageBackground = await page.Locator("section").EvaluateAsync<string>("element => getComputedStyle(element).backgroundImage");
        var layout = await page.EvaluateAsync<ViewportLayout>("""
            () => {
                const card = document.querySelector('.login-card, .identity-card');
                const rect = card.getBoundingClientRect();
                return {
                    viewportWidth: window.innerWidth,
                    documentWidth: document.documentElement.scrollWidth,
                    cardLeft: rect.left,
                    cardRight: rect.right
                };
            }
            """);

        Assert.Equal("rgb(255, 255, 255)", cardBackground);
        Assert.Contains("radial-gradient", pageBackground, StringComparison.OrdinalIgnoreCase);
        Assert.True(layout.DocumentWidth <= layout.ViewportWidth, "A página não deve criar rolagem horizontal em celular.");
        Assert.True(layout.CardLeft >= 0 && layout.CardRight <= layout.ViewportWidth, "O cartão deve caber na largura da tela.");
    }

    [Fact]
    public async Task LoginMostraCamposEValidaCamposObrigatorios()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseAddress);

        Assert.True(await page.GetByLabel("E-mail").IsVisibleAsync());
        Assert.True(await page.GetByRole(AriaRole.Textbox, new() { Name = "Senha", Exact = true }).IsVisibleAsync());
        await page.GetByRole(AriaRole.Button, new() { Name = "Entrar" }).ClickAsync();

        await page.GetByText("Informe seu e-mail.", new() { Exact = true }).First.WaitForAsync();
        var messages = await page.Locator(".login-field-error").AllTextContentsAsync();
        Assert.Contains("Informe seu e-mail.", messages);
        Assert.Contains("Informe sua senha.", messages);
    }

    [Fact]
    public async Task LoginRejeitaEmailMalformado()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseAddress);

        var email = page.GetByLabel("E-mail");
        await email.FillAsync("nao-e-um-email");

        Assert.True(await email.EvaluateAsync<bool>("element => element.validity.typeMismatch"));
    }

    [Theory]
    [InlineData("/", "login-password")]
    [InlineData("/criar-conta", "register-password")]
    [InlineData("/criar-conta", "register-confirm-password")]
    public async Task AlternaVisibilidadeDosCamposDeSenha(string route, string inputId)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), route.TrimStart('/')).AbsoluteUri);

        var input = page.Locator($"#{inputId}");
        var toggle = page.Locator($"button[aria-controls='{inputId}']");
        Assert.Equal("password", await input.GetAttributeAsync("type"));

        await toggle.ClickAsync();
        Assert.Equal("text", await input.GetAttributeAsync("type"));
        Assert.Equal("Ocultar senha", await toggle.GetAttributeAsync("aria-label"));

        await toggle.ClickAsync();
        Assert.Equal("password", await input.GetAttributeAsync("type"));
        Assert.Equal("Mostrar senha", await toggle.GetAttributeAsync("aria-label"));
    }

    [Theory]
    [InlineData("Esqueceu a senha?", "/esqueci-a-senha")]
    [InlineData("Criar uma conta", "/criar-conta")]
    public async Task LinksDoLoginAbremAsRotasCorretas(string linkName, string expectedRoute)
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseAddress);

        await page.GetByRole(AriaRole.Link, new() { Name = linkName }).ClickAsync();
        var destinationHeading = expectedRoute == "/esqueci-a-senha"
            ? "Esqueceu sua senha?"
            : "Criar sua conta";
        await page.GetByRole(AriaRole.Heading, new() { Name = destinationHeading }).WaitForAsync();

        Assert.Equal(expectedRoute, new Uri(page.Url).AbsolutePath);
    }

    [Fact]
    public async Task RecuperacaoExigeEmail()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), "esqueci-a-senha").AbsoluteUri);

        var email = page.GetByLabel("E-mail");
        Assert.True(await email.IsVisibleAsync());
        await page.GetByRole(AriaRole.Button, new() { Name = "Enviar link de recuperação" }).ClickAsync();
        await page.GetByText("Informe seu e-mail.", new() { Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task RecuperacaoRejeitaEmailMalformado()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), "esqueci-a-senha").AbsoluteUri);

        var email = page.GetByLabel("E-mail");
        await email.FillAsync("nao-e-um-email");
        Assert.True(await email.EvaluateAsync<bool>("element => element.validity.typeMismatch"));
    }

    [Fact]
    public async Task RecuperacaoComEmailValidoMostraRespostaGenericaSemConta()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), "esqueci-a-senha").AbsoluteUri);

        await page.GetByLabel("E-mail").FillAsync("nao-existe@example.test");
        await page.GetByRole(AriaRole.Button, new() { Name = "Enviar link de recuperação" }).ClickAsync();
        var response = page.GetByRole(AriaRole.Status);
        try
        {
            await response.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            var body = await page.Locator("body").InnerTextAsync();
            throw new InvalidOperationException(
                $"A resposta de recuperação não apareceu. URL={page.Url}{Environment.NewLine}Conteúdo={body}{Environment.NewLine}Log da aplicação={fixture.ServerOutput}");
        }

        Assert.Contains("Se houver uma conta confirmada para esse endereço", await response.InnerTextAsync());
    }

    [Fact]
    public async Task CadastroExigeEmailSenhaEConfirmacao()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), "criar-conta").AbsoluteUri);

        var email = page.GetByLabel("E-mail");
        var password = page.GetByRole(AriaRole.Textbox, new() { Name = "Senha", Exact = true });
        var confirmation = page.GetByRole(AriaRole.Textbox, new() { Name = "Confirmar senha", Exact = true });
        Assert.True(await email.IsVisibleAsync());
        Assert.True(await password.IsVisibleAsync());
        Assert.True(await confirmation.IsVisibleAsync());

        await page.GetByRole(AriaRole.Button, new() { Name = "Cadastrar novo usuário" }).ClickAsync();
        await page.GetByText("Informe seu e-mail.", new() { Exact = true }).WaitForAsync();
        var requiredMessages = await page.Locator(".validation-message").AllTextContentsAsync();
        Assert.Contains("Informe seu e-mail.", requiredMessages);
        Assert.Contains("Informe uma senha.", requiredMessages);
        Assert.Contains("Confirme sua senha.", requiredMessages);
    }

    [Fact]
    public async Task CadastroRejeitaSenhasDiferentes()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), "criar-conta").AbsoluteUri);

        await page.GetByRole(AriaRole.Textbox, new() { Name = "E-mail", Exact = true }).FillAsync("browser-test@example.test");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Senha", Exact = true }).FillAsync("SenhaForte123!");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Confirmar senha", Exact = true }).FillAsync("SenhaForte123?");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cadastrar novo usuário" }).ClickAsync();
        await page.GetByText("As senhas não coincidem.", new() { Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task CadastroRejeitaSenhaSemCaractereEspecial()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(new Uri(fixture.BaseAddress), "criar-conta").AbsoluteUri);

        var email = page.GetByRole(AriaRole.Textbox, new() { Name = "E-mail", Exact = true });
        var password = page.GetByRole(AriaRole.Textbox, new() { Name = "Senha", Exact = true });
        var confirmation = page.GetByRole(AriaRole.Textbox, new() { Name = "Confirmar senha", Exact = true });
        await email.FillAsync("browser-test@example.test");
        await password.FillAsync("SenhaForte1234");
        await confirmation.FillAsync("SenhaForte1234");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cadastrar novo usuário" }).ClickAsync();
        var identityError = page.Locator(".identity-error");
        await identityError.WaitForAsync();

        Assert.Contains("non alphanumeric", await identityError.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ViewportLayout
    {
        public double ViewportWidth { get; set; }
        public double DocumentWidth { get; set; }
        public double CardLeft { get; set; }
        public double CardRight { get; set; }
    }
}
