document.addEventListener("DOMContentLoaded", () => {
  const button = document.getElementById("toggle-login-password");
  const input = document.getElementById("Input_Password");
  if (!button || !input) return;

  button.addEventListener("click", () => {
    const visible = input.type !== "password";
    input.type = visible ? "password" : "text";
    button.setAttribute("aria-pressed", String(!visible));
    button.setAttribute("aria-label", visible ? "Mostrar contraseña" : "Ocultar contraseña");
    button.title = visible ? "Mostrar contraseña" : "Ocultar contraseña";
    button.classList.toggle("is-visible", !visible);
  });
});
