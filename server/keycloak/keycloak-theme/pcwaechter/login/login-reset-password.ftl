<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('username'); section>
    <#if section = "header">
        PCWächter Passwort zurücksetzen
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
          <h1 class="kc-title">Passwort vergessen?</h1>
          <p class="kc-subtitle">Setzen Sie Ihr Passwort zurück</p>
        </div>

        <!-- Reset Password Form -->
        <form id="kc-reset-password-form" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">
          
          <!-- Info Box -->
          <div class="kc-info-box">
            <div class="kc-info-icon">
              <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>
                <path d="M12 16v-4M12 8h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
            </div>
            <div>
              <p class="kc-info-title">Passwort zurücksetzen</p>
              <p class="kc-info-text">Geben Sie Ihre E-Mail-Adresse ein. Wir senden Ihnen einen Link zum Zurücksetzen Ihres Passworts.</p>
            </div>
          </div>

          <!-- Error Messages -->
          <#if messagesPerField.existsError('username')>
            <div class="kc-error-message">
              <svg class="kc-error-icon" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>
                <path d="M12 8v4M12 16h.01" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <span>${kcSanitize(messagesPerField.get('username'))?no_esc}</span>
            </div>
          </#if>

          <!-- Username/Email Field -->
          <div class="kc-form-group">
            <label for="username" class="kc-label">
              <#if !realm.loginWithEmailAllowed>Benutzername<#else>E-Mail</#if>
              <span class="kc-required">*</span>
            </label>
            <input 
              tabindex="1"
              type="text" 
              id="username" 
              name="username" 
              class="kc-input <#if messagesPerField.existsError('username')>kc-input-error</#if>" 
              autofocus
              autocomplete="username"
              placeholder="name@beispiel.de"
              value="${(auth.attemptedUsername!'')}"
            />
          </div>

          <!-- Submit Button -->
          <div class="kc-form-buttons">
            <button 
              tabindex="2"
              class="kc-btn kc-btn-primary" 
              type="submit"
            >
              <span class="kc-btn-text">Link zum Zurücksetzen senden</span>
              <div class="kc-btn-spinner">
                <div class="kc-spinner"></div>
              </div>
            </button>
          </div>

          <!-- Back to Login Link -->
          <div class="kc-form-footer kc-footer-center">
            <a tabindex="3" href="${url.loginUrl}" class="kc-link">Zurück zur Anmeldung</a>
          </div>
        </form>
      </div>
    </div>
    </#if>
</@layout.registrationLayout>
