<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('firstName','lastName','email','username','password','password-confirm'); section>
    <#if section = "header">
        PCWächter Registrierung
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
          <h1 class="kc-title">Registrierung</h1>
          <p class="kc-subtitle">Erstellen Sie Ihr PCWächter-Konto</p>
        </div>

        <!-- Registration Form -->
        <form id="kc-register-form" class="${properties.kcFormClass!}" action="${url.registrationAction}" method="post">
          
          <!-- Error Messages -->
          <#if messagesPerField.existsError('firstName','lastName','email','username','password','password-confirm')>
            <div class="kc-error-message">
              <svg class="kc-error-icon" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>
                <path d="M12 8v4M12 16h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <span>
                <#if messagesPerField.existsError('firstName')>
                  ${kcSanitize(messagesPerField.get('firstName'))?no_esc}
                <#elseif messagesPerField.existsError('lastName')>
                  ${kcSanitize(messagesPerField.get('lastName'))?no_esc}
                <#elseif messagesPerField.existsError('email')>
                  ${kcSanitize(messagesPerField.get('email'))?no_esc}
                <#elseif messagesPerField.existsError('username')>
                  ${kcSanitize(messagesPerField.get('username'))?no_esc}
                <#elseif messagesPerField.existsError('password')>
                  ${kcSanitize(messagesPerField.get('password'))?no_esc}
                <#elseif messagesPerField.existsError('password-confirm')>
                  ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
                </#if>
              </span>
            </div>
          </#if>

          <!-- First Name -->
          <div class="kc-form-group">
            <label for="firstName" class="kc-label">Vorname</label>
            <input 
              tabindex="1"
              type="text" 
              id="firstName" 
              class="kc-input <#if messagesPerField.existsError('firstName')>kc-input-error</#if>" 
              name="firstName"
              value="${(register.formData.firstName!'')}"
              autocomplete="given-name"
              placeholder="Max"
            />
          </div>

          <!-- Last Name -->
          <div class="kc-form-group">
            <label for="lastName" class="kc-label">Nachname</label>
            <input 
              tabindex="2"
              type="text" 
              id="lastName" 
              class="kc-input <#if messagesPerField.existsError('lastName')>kc-input-error</#if>" 
              name="lastName"
              value="${(register.formData.lastName!'')}"
              autocomplete="family-name"
              placeholder="Mustermann"
            />
          </div>

          <!-- Email -->
          <div class="kc-form-group">
            <label for="email" class="kc-label">
              E-Mail
              <span class="kc-required">*</span>
            </label>
            <input 
              tabindex="3"
              type="email" 
              id="email" 
              class="kc-input <#if messagesPerField.existsError('email')>kc-input-error</#if>" 
              name="email"
              value="${(register.formData.email!'')}"
              autocomplete="email"
              autofocus
              placeholder="name@beispiel.de"
            />
          </div>

          <!-- Username (if not email as username) -->
          <#if !realm.registrationEmailAsUsername>
            <div class="kc-form-group">
              <label for="username" class="kc-label">
                Benutzername
                <span class="kc-required">*</span>
              </label>
              <input 
                tabindex="4"
                type="text" 
                id="username" 
                class="kc-input <#if messagesPerField.existsError('username')>kc-input-error</#if>" 
                name="username"
                value="${(register.formData.username!'')}"
                autocomplete="username"
                placeholder="benutzername"
              />
            </div>
          </#if>

          <!-- Password -->
          <#if passwordRequired??>
            <div class="kc-form-group">
              <label for="password" class="kc-label">
                Passwort
                <span class="kc-required">*</span>
              </label>
              <div class="kc-password-wrapper">
                <input 
                  tabindex="5"
                  type="password" 
                  id="password" 
                  class="kc-input <#if messagesPerField.existsError('password')>kc-input-error</#if>" 
                  name="password"
                  autocomplete="new-password"
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

            <!-- Password Confirm -->
            <div class="kc-form-group">
              <label for="password-confirm" class="kc-label">
                Passwort bestätigen
                <span class="kc-required">*</span>
              </label>
              <div class="kc-password-wrapper">
                <input 
                  tabindex="6"
                  type="password" 
                  id="password-confirm" 
                  class="kc-input <#if messagesPerField.existsError('password-confirm')>kc-input-error</#if>" 
                  name="password-confirm"
                  autocomplete="new-password"
                  placeholder="••••••••"
                />
                <button type="button" class="kc-password-toggle" onclick="togglePassword('password-confirm')">
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
          </#if>

          <!-- Recaptcha -->
          <#if recaptchaRequired??>
            <div class="kc-form-group">
              <div class="g-recaptcha" data-size="compact" data-sitekey="${recaptchaSiteKey}"></div>
            </div>
          </#if>

          <!-- Submit Button -->
          <div class="kc-form-buttons">
            <button 
              tabindex="7"
              class="kc-btn kc-btn-primary" 
              type="submit"
            >
              <span class="kc-btn-text">Registrieren</span>
              <div class="kc-btn-spinner">
                <div class="kc-spinner"></div>
              </div>
            </button>
          </div>

          <!-- Back to Login Link -->
          <div class="kc-form-footer kc-footer-center">
            <a tabindex="8" href="${url.loginUrl}" class="kc-link">Zurück zur Anmeldung</a>
          </div>
        </form>
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
      
      // Initial: Alle Eye-Open Icons verstecken
      document.addEventListener('DOMContentLoaded', function() {
        var eyeOpenIcons = document.querySelectorAll('.kc-eye-open');
        eyeOpenIcons.forEach(function(icon) {
          icon.style.display = 'none';
        });
      });
    </script>
    </#if>
</@layout.registrationLayout>
