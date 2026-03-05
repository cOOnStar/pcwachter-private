package de.pcwaechter.keycloak;

import jakarta.ws.rs.core.MultivaluedMap;
import jakarta.ws.rs.core.Response;
import java.util.Arrays;
import java.util.List;
import java.util.Set;
import java.util.stream.Collectors;
import java.util.regex.Pattern;
import org.keycloak.authentication.AuthenticationFlowContext;
import org.keycloak.authentication.AuthenticationFlowError;
import org.keycloak.authentication.authenticators.browser.UsernamePasswordForm;
import org.keycloak.models.utils.FormMessage;
import org.keycloak.sessions.AuthenticationSessionModel;

public class LicenseKeyUsernamePasswordForm extends UsernamePasswordForm {

    private static final String LICENSE_KEY_PARAM = "licenseKey";
    private static final String LICENSE_KEYS_ENV = "PCW_LICENSE_KEYS";
    private static final Pattern LICENSE_PATTERN = Pattern.compile("^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$");

    @Override
    protected boolean validateForm(AuthenticationFlowContext context, MultivaluedMap<String, String> formData) {
        String submitted = formData.getFirst(LICENSE_KEY_PARAM);
        String normalized = normalizeLicenseKey(submitted);

        if (normalized == null || normalized.isBlank()) {
            return super.validateForm(context, formData);
        }

        if (!isValidLicenseKey(normalized)) {
            List<FormMessage> errors = List.of(new FormMessage(LICENSE_KEY_PARAM, "invalidLicenseKeyFormat"));
            Response challenge = context.form()
                    .setErrors(errors)
                    .setAttribute(LICENSE_KEY_PARAM, submitted == null ? "" : submitted)
                    .createLoginUsernamePassword();
            context.failureChallenge(AuthenticationFlowError.INVALID_CREDENTIALS, challenge);
            return false;
        }

        if (!isAllowedLicenseKey(normalized)) {
            List<FormMessage> errors = List.of(new FormMessage(LICENSE_KEY_PARAM, "invalidLicenseKey"));
            Response challenge = context.form()
                .setErrors(errors)
                .setAttribute(LICENSE_KEY_PARAM, normalized)
                .createLoginUsernamePassword();
            context.failureChallenge(AuthenticationFlowError.INVALID_CREDENTIALS, challenge);
            return false;
        }

        formData.putSingle(LICENSE_KEY_PARAM, normalized);
        AuthenticationSessionModel session = context.getAuthenticationSession();
        session.setUserSessionNote(LICENSE_KEY_PARAM, normalized);
        session.setAuthNote(LICENSE_KEY_PARAM, normalized);

        return super.validateForm(context, formData);
    }

    private String normalizeLicenseKey(String input) {
        if (input == null) {
            return "";
        }
        String alnum = input.toUpperCase().replaceAll("[^A-Z0-9]", "");
        if (alnum.length() > 12) {
            alnum = alnum.substring(0, 12);
        }

        StringBuilder out = new StringBuilder();
        for (int i = 0; i < alnum.length(); i++) {
            if (i > 0 && i % 4 == 0) {
                out.append('-');
            }
            out.append(alnum.charAt(i));
        }
        return out.toString();
    }

    private boolean isValidLicenseKey(String value) {
        return value != null && LICENSE_PATTERN.matcher(value).matches();
    }

    private boolean isAllowedLicenseKey(String normalized) {
        Set<String> configured = configuredLicenseKeys();
        if (configured.isEmpty()) {
            return true;
        }
        return configured.contains(normalized);
    }

    private Set<String> configuredLicenseKeys() {
        String raw = System.getenv(LICENSE_KEYS_ENV);
        if (raw == null || raw.isBlank()) {
            return Set.of();
        }
        return Arrays.stream(raw.split("[,;\\s]+"))
                .map(this::normalizeLicenseKey)
                .filter(this::isValidLicenseKey)
                .collect(Collectors.toSet());
    }
}
