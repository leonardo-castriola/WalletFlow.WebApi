document.addEventListener("click", (event) => {
    const toggle = event.target.closest("[data-password-toggle]");
    if (!toggle) return;

    const input = document.getElementById(toggle.getAttribute("aria-controls"));
    if (!(input instanceof HTMLInputElement)) return;

    const showPassword = input.type === "password";
    input.type = showPassword ? "text" : "password";
    toggle.setAttribute("aria-pressed", String(showPassword));
    toggle.setAttribute("aria-label", showPassword ? "Ocultar senha" : "Mostrar senha");
    toggle.querySelector(".password-eye").hidden = showPassword;
    toggle.querySelector(".password-eye-off").hidden = !showPassword;
});
