// ByDesign API v2 - Java sample
//
// Requires: Java 11+ (no external dependencies). Run with: java ByDesignApiSample.java
// Set these environment variables first (values come from the ByDesign back office):
//   BYDESIGN_API_URL        e.g. https://your-api-url (no trailing slash)
//   BYDESIGN_CLIENT_ID
//   BYDESIGN_CLIENT_SECRET
//   BYDESIGN_API_KEY
//
// This sample parses JSON with a regular expression to stay dependency-free.
// In a real application, use a JSON library such as Jackson or Gson.

import java.io.IOException;
import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.Instant;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

public class ByDesignApiSample {

    public static void main(String[] args) throws Exception {
        ByDesignClient client = new ByDesignClient(
                required("BYDESIGN_API_URL"),
                required("BYDESIGN_CLIENT_ID"),
                required("BYDESIGN_CLIENT_SECRET"),
                required("BYDESIGN_API_KEY"));

        // GET request: list inventory categories (requires api/Read)
        System.out.println(client.send("GET", "/api/v2/inventory/categorydetail", null));

        // POST request: look up customers by email (requires api/Write)
        System.out.println(client.send("POST", "/api/v2/customer/lookup", "{\"Email\": \"jane.doe@example.com\"}"));
    }

    private static String required(String name) {
        String value = System.getenv(name);
        if (value == null || value.isEmpty()) {
            throw new IllegalStateException("Set the " + name + " environment variable.");
        }
        return value;
    }

    /** Minimal ByDesign API client that caches its access token and retries on 401 and 429. */
    static class ByDesignClient {
        private final HttpClient http = HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(30)).build();
        private final String apiUrl;
        private final String clientId;
        private final String clientSecret;
        private final String apiKey;
        private String accessToken;
        private Instant expiresAt = Instant.EPOCH;

        ByDesignClient(String apiUrl, String clientId, String clientSecret, String apiKey) {
            this.apiUrl = apiUrl.replaceAll("/+$", "");
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.apiKey = apiKey;
        }

        String send(String method, String path, String jsonBody) throws IOException, InterruptedException {
            for (int attempt = 1; ; attempt++) {
                HttpRequest.Builder builder = HttpRequest.newBuilder(URI.create(apiUrl + path))
                        .header("Authorization", "Bearer " + getToken())
                        .header("x-api-key", apiKey);
                if (jsonBody != null) {
                    builder.header("Content-Type", "application/json")
                            .method(method, HttpRequest.BodyPublishers.ofString(jsonBody));
                } else {
                    builder.method(method, HttpRequest.BodyPublishers.noBody());
                }

                HttpResponse<String> response = http.send(builder.build(), HttpResponse.BodyHandlers.ofString());

                if (response.statusCode() == 401 && attempt == 1) {
                    invalidateToken(); // Token expired or was revoked: fetch a new one and retry once
                    continue;
                }
                if (response.statusCode() == 429 && attempt <= 3) {
                    Thread.sleep((long) Math.pow(2, attempt) * 1000);
                    continue;
                }
                if (response.statusCode() >= 400) {
                    throw new IOException(method + " " + path + " failed: " + response.statusCode() + " " + response.body());
                }
                return response.body();
            }
        }

        private synchronized String getToken() throws IOException, InterruptedException {
            // Reuse the token until one minute before it expires
            if (accessToken != null && Instant.now().isBefore(expiresAt.minusSeconds(60))) {
                return accessToken;
            }

            String form = Map.of(
                            "grant_type", "client_credentials",
                            "client_id", clientId,
                            "client_secret", clientSecret,
                            "scope", "api/Read api/Write")
                    .entrySet().stream()
                    .map(e -> e.getKey() + "=" + URLEncoder.encode(e.getValue(), StandardCharsets.UTF_8))
                    .collect(Collectors.joining("&"));

            HttpRequest request = HttpRequest.newBuilder(URI.create(apiUrl + "/oauth2/token"))
                    .header("Content-Type", "application/x-www-form-urlencoded")
                    .POST(HttpRequest.BodyPublishers.ofString(form))
                    .build();
            HttpResponse<String> response = http.send(request, HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() != 200) {
                throw new IOException("Token request failed: " + response.statusCode() + " " + response.body());
            }

            accessToken = jsonValue(response.body(), "access_token");
            expiresAt = Instant.now().plusSeconds(Long.parseLong(jsonValue(response.body(), "expires_in")));
            return accessToken;
        }

        private synchronized void invalidateToken() {
            accessToken = null;
        }

        private static String jsonValue(String json, String key) {
            Matcher m = Pattern.compile("\"" + key + "\"\\s*:\\s*\"?([^\",}]+)").matcher(json);
            if (!m.find()) throw new IllegalStateException("Missing " + key + " in token response");
            return m.group(1).trim();
        }
    }
}
