FROM maven:3.9.9-eclipse-temurin-17 AS provider-build
WORKDIR /build
COPY providers/license-key-authenticator /build/providers/license-key-authenticator
RUN mvn -f /build/providers/license-key-authenticator/pom.xml -DskipTests package

FROM quay.io/keycloak/keycloak:26.5.4
COPY --from=provider-build /build/providers/license-key-authenticator/target/license-key-authenticator-1.0.0.jar /opt/keycloak/providers/
