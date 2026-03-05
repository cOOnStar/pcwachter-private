<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('username','password','licenseKey') displayInfo=realm.password && realm.registrationAllowed && !registrationDisabled??; section>
    <#if section = "header">
        PCWächter Anmeldung
    <#elseif section = "form">
    <div id="kc-form">
      <div id="kc-form-wrapper">
        
        <!-- Logo Header -->
        <div class="kc-logo-wrapper">
          <div class="kc-logo-glow"></div>
          <div class="kc-logo-container">
            <!-- PCWächter Logo aus Theme-Ressourcen -->
            <img src="${url.resourcesPath}/img/logo.png" alt="PCWächter Logo" class="kc-logo-img" />
          </div>
        </div>

        <!-- Title -->
        <div class="kc-title-wrapper">
          <h1 class="kc-title">PCWächter</h1>
          <p class="kc-subtitle">Sicher und geschützt</p>
        </div>

        <!-- Social Providers -->
        <#if realm.password && social.providers??>
          <div class="kc-social-section">
            <#list social.providers as p>
              <a id="social-${p.alias}" class="kc-social-btn kc-social-${p.alias}" href="${p.loginUrl}">
                <span class="kc-social-icon">
                  <#if p.alias == "google">
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                      <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                      <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                      <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                      <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
                    </svg>
                  <#elseif p.alias == "microsoft">
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                      <path fill="#f25022" d="M11.4 11.4H2V2h9.4z"/>
                      <path fill="#00a4ef" d="M22 11.4h-9.4V2H22z"/>
                      <path fill="#7fba00" d="M11.4 22H2v-9.4h9.4z"/>
                      <path fill="#ffb900" d="M22 22h-9.4v-9.4H22z"/>
                    </svg>
                  <#elseif p.alias == "github">
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                      <path fill="#181717" d="M12 2C6.477 2 2 6.477 2 12c0 4.42 2.865 8.17 6.839 9.49.5.092.682-.217.682-.482 0-.237-.008-.866-.013-1.7-2.782.603-3.369-1.34-3.369-1.34-.454-1.156-1.11-1.463-1.11-1.463-.908-.62.069-.608.069-.608 1.003.07 1.531 1.03 1.531 1.03.892 1.529 2.341 1.087 2.91.831.092-.646.35-1.086.636-1.336-2.22-.253-4.555-1.11-4.555-4.943 0-1.091.39-1.984 1.029-2.683-.103-.253-.446-1.27.098-2.647 0 0 .84-.269 2.75 1.025A9.578 9.578 0 0112 6.836c.85.004 1.705.114 2.504.336 1.909-1.294 2.747-1.025 2.747-1.025.546 1.377.203 2.394.1 2.647.64.699 1.028 1.592 1.028 2.683 0 3.842-2.339 4.687-4.566 4.935.359.309.678.919.678 1.852 0 1.336-.012 2.415-.012 2.743 0 .267.18.578.688.48C19.138 20.167 22 16.418 22 12c0-5.523-4.477-10-10-10z"/>
                    </svg>
                  <#elseif p.alias == "facebook">
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                      <path fill="#1877F2" d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>
                    </svg>
                  <#else>
                    <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                      <circle cx="12" cy="12" r="10" fill="#6366f1"/>
                    </svg>
                  </#if>
                </span>
                <span class="kc-social-text">Mit ${p.displayName!} anmelden</span>
              </a>
            </#list>
          </div>
        </#if>

        <!-- Divider -->
        <#if realm.password && social.providers??>
          <div class="kc-divider">
            <div class="kc-divider-line"></div>
            <span class="kc-divider-text">Oder mit Konto</span>
            <div class="kc-divider-line"></div>
          </div>
        </#if>

        <!-- Login Form -->
        <#if realm.password>
          <form id="kc-form-login" onsubmit="return handleLoginSubmit();" action="${url.loginAction}" method="post">
            
            <!-- Error Messages -->
            <#if messagesPerField.existsError('username','password','licenseKey')>
              <div class="kc-error-message">
                <svg class="kc-error-icon" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                  <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>
                  <path d="M12 8v4M12 16h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
                </svg>
                <span>${kcSanitize(messagesPerField.getFirstError('username','password','licenseKey'))?no_esc}</span>
              </div>
            </#if>

            <!-- Username Field -->
            <div class="kc-form-group">
              <label for="username" class="kc-label">
                <#if !realm.loginWithEmailAllowed>Benutzername<#elseif !realm.registrationEmailAsUsername>Benutzername oder E-Mail<#else>E-Mail</#if>
                <span class="kc-required">*</span>
              </label>
              <input 
                tabindex="1" 
                id="username" 
                class="kc-input <#if messagesPerField.existsError('username','password')>kc-input-error</#if>" 
                name="username" 
                value="${(login.username!'')}" 
                type="text" 
                autofocus 
                autocomplete="username"
                aria-invalid="<#if messagesPerField.existsError('username','password')>true</#if>"
                placeholder="name@beispiel.de"
              />
            </div>

            <!-- License Key Field -->
            <div class="kc-form-group">
              <label for="licenseKey" class="kc-label">
                Lizenzschlüssel
              </label>
              <input
                tabindex="2"
                id="licenseKey"
                class="kc-input"
                name="licenseKey"
                value="${(licenseKey!'')}"
                type="text"
                inputmode="text"
                autocapitalize="characters"
                autocomplete="off"
                maxlength="14"
                pattern="[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}"
                placeholder="XXXX-XXXX-XXXX"
                aria-describedby="kc-license-help"
              />
              <p id="kc-license-help" class="kc-help-text">Format: XXXX-XXXX-XXXX</p>
            </div>

            <!-- Password Field -->
            <div class="kc-form-group">
              <label for="password" class="kc-label">
                Passwort
                <span class="kc-required">*</span>
              </label>
              <div class="kc-password-wrapper">
                <input 
                  tabindex="3" 
                  id="password" 
                  class="kc-input <#if messagesPerField.existsError('username','password')>kc-input-error</#if>" 
                  name="password" 
                  type="password" 
                  autocomplete="current-password"
                  aria-invalid="<#if messagesPerField.existsError('username','password')>true</#if>"
                  placeholder="••••••••"
                />
                <button type="button" class="kc-password-toggle" onclick="togglePassword('password')">
                  <svg class="kc-eye-icon kc-eye-closed" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                    <circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>
                  </svg>
                  <svg class="kc-eye-icon kc-eye-open" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-10-8-10-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 10 8 10 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24M1 1l22 22" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                  </svg>
                </button>
              </div>
            </div>

            <!-- Remember Me -->
            <#if realm.rememberMe && !usernameHidden??>
              <div class="kc-form-options">
                <div class="kc-checkbox-wrapper">
                  <input 
                    tabindex="4" 
                    id="rememberMe" 
                    name="rememberMe" 
                    type="checkbox" 
                    class="kc-checkbox"
                    <#if login.rememberMe??>checked</#if>
                  />
                  <label for="rememberMe" class="kc-checkbox-label">Angemeldet bleiben</label>
                </div>
              </div>
            </#if>

            <!-- Hidden Credential Field -->
            <input 
              type="hidden" 
              id="id-hidden-input" 
              name="credentialId" 
              <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>
            />

            <!-- Submit Button -->
            <div class="kc-form-buttons">
              <button 
                tabindex="5" 
                class="kc-btn kc-btn-primary" 
                name="login" 
                id="kc-login" 
                type="submit"
              >
                <span class="kc-btn-text">Anmelden</span>
                <div class="kc-btn-spinner">
                  <div class="kc-spinner"></div>
                </div>
              </button>
            </div>

            <!-- Footer Links -->
            <div class="kc-form-footer">
              <#if realm.resetPasswordAllowed>
                <a tabindex="6" href="${url.loginResetCredentialsUrl}" class="kc-link" onclick="return showForgotPasswordPanel(event)">Passwort vergessen?</a>
              </#if>
              <#if realm.password && realm.registrationAllowed && !registrationDisabled??>
                <a tabindex="7" href="${url.registrationUrl}" class="kc-link" onclick="return showRegisterPanel(event)">Registrieren</a>
              </#if>
            </div>
          </form>

          <div id="kc-register-panel" class="kc-alt-panel kc-hidden" aria-hidden="true">
            <div class="kc-alt-panel-header">
              <h2 class="kc-alt-panel-title">Registrierung</h2>
              <button type="button" class="kc-alt-panel-close" onclick="showLoginPanel()">Zurück</button>
            </div>
            <iframe
              id="kc-register-iframe"
              class="kc-alt-panel-frame"
              data-src="${url.registrationUrl}"
              src="about:blank"
              title="Registrierung"
            ></iframe>
          </div>

          <div id="kc-forgot-panel" class="kc-alt-panel kc-hidden" aria-hidden="true">
            <div class="kc-alt-panel-header">
              <h2 class="kc-alt-panel-title">Passwort vergessen?</h2>
              <button type="button" class="kc-alt-panel-close" onclick="showLoginPanel()">Zurück</button>
            </div>
            <iframe
              id="kc-forgot-iframe"
              class="kc-alt-panel-frame"
              data-src="${url.loginResetCredentialsUrl}"
              src="about:blank"
              title="Passwort zurücksetzen"
            ></iframe>
          </div>
        </#if>
      </div>
    </div>

    <!-- Password Toggle Script -->
    <script>
      function togglePassword(fieldId) {
        var passwordInput = document.getElementById(fieldId);
        if (!passwordInput) return;
        
        var wrapper = passwordInput.parentElement;
        var toggleBtn = wrapper.querySelector('.kc-password-toggle');
        var eyeClosed = toggleBtn.querySelector('.kc-eye-closed');
        var eyeOpen = toggleBtn.querySelector('.kc-eye-open');
        
        if (passwordInput.type === 'password') {
          passwordInput.type = 'text';
          eyeClosed.style.display = 'none';
          eyeOpen.style.display = 'block';
        } else {
          passwordInput.type = 'password';
          eyeClosed.style.display = 'block';
          eyeOpen.style.display = 'none';
        }
      }

      function formatLicenseKey(value) {
        var clean = (value || '').toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 12);
        var parts = [];
        for (var i = 0; i < clean.length; i += 4) {
          parts.push(clean.slice(i, i + 4));
        }
        return parts.join('-');
      }

      function handleLoginSubmit() {
        var form = document.getElementById('kc-form-login');
        var loginButton = document.getElementById('kc-login');
        var licenseInput = document.getElementById('licenseKey');

        if (licenseInput) {
          var formatted = formatLicenseKey(licenseInput.value);
          licenseInput.value = formatted;
          var isValid = formatted.length === 14 && /^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$/.test(formatted);
          if (!isValid) {
            licenseInput.setCustomValidity('Bitte Lizenzschlüssel im Format XXXX-XXXX-XXXX eingeben.');
            licenseInput.reportValidity();
            return false;
          }
          licenseInput.setCustomValidity('');
        }

        if (loginButton) {
          loginButton.disabled = true;
        }
        if (form) {
          return true;
        }
        return false;
      }

      function loadIframeIfNeeded(iframeId) {
        var iframe = document.getElementById(iframeId);
        if (!iframe) return;
        if (iframe.src === 'about:blank') {
          var src = iframe.getAttribute('data-src');
          if (src) iframe.src = src;
        }
      }

      function showLoginPanel() {
        var loginForm = document.getElementById('kc-form-login');
        var registerPanel = document.getElementById('kc-register-panel');
        var forgotPanel = document.getElementById('kc-forgot-panel');
        if (loginForm) loginForm.classList.remove('kc-hidden');
        if (registerPanel) {
          registerPanel.classList.add('kc-hidden');
          registerPanel.setAttribute('aria-hidden', 'true');
        }
        if (forgotPanel) {
          forgotPanel.classList.add('kc-hidden');
          forgotPanel.setAttribute('aria-hidden', 'true');
        }
      }

      function showRegisterPanel(event) {
        if (event) event.preventDefault();
        var loginForm = document.getElementById('kc-form-login');
        var registerPanel = document.getElementById('kc-register-panel');
        var forgotPanel = document.getElementById('kc-forgot-panel');
        if (loginForm) loginForm.classList.add('kc-hidden');
        if (forgotPanel) {
          forgotPanel.classList.add('kc-hidden');
          forgotPanel.setAttribute('aria-hidden', 'true');
        }
        if (registerPanel) {
          registerPanel.classList.remove('kc-hidden');
          registerPanel.setAttribute('aria-hidden', 'false');
        }
        loadIframeIfNeeded('kc-register-iframe');
        return false;
      }

      function showForgotPasswordPanel(event) {
        if (event) event.preventDefault();
        var loginForm = document.getElementById('kc-form-login');
        var registerPanel = document.getElementById('kc-register-panel');
        var forgotPanel = document.getElementById('kc-forgot-panel');
        if (loginForm) loginForm.classList.add('kc-hidden');
        if (registerPanel) {
          registerPanel.classList.add('kc-hidden');
          registerPanel.setAttribute('aria-hidden', 'true');
        }
        if (forgotPanel) {
          forgotPanel.classList.remove('kc-hidden');
          forgotPanel.setAttribute('aria-hidden', 'false');
        }
        loadIframeIfNeeded('kc-forgot-iframe');
        return false;
      }
      
      // Initial: Alle Eye-Open Icons verstecken
      document.addEventListener('DOMContentLoaded', function() {
        var eyeOpenIcons = document.querySelectorAll('.kc-eye-open');
        eyeOpenIcons.forEach(function(icon) {
          icon.style.display = 'none';
        });

        var licenseInput = document.getElementById('licenseKey');
        if (licenseInput) {
          licenseInput.addEventListener('input', function() {
            var cursorPos = licenseInput.selectionStart;
            var beforeLength = licenseInput.value.length;
            licenseInput.value = formatLicenseKey(licenseInput.value);
            var afterLength = licenseInput.value.length;
            var nextPos = cursorPos + (afterLength - beforeLength);
            licenseInput.setSelectionRange(nextPos, nextPos);
            licenseInput.setCustomValidity('');
          });

          licenseInput.addEventListener('blur', function() {
            licenseInput.value = formatLicenseKey(licenseInput.value);
          });
        }
      });
    </script>
    </#if>
</@layout.registrationLayout>
