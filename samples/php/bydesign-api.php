<?php
// ByDesign API v2 - PHP sample
//
// Requires: PHP 8.0+ with the curl and json extensions. Run with: php bydesign-api.php
// Set these environment variables first (values come from the ByDesign back office):
//   BYDESIGN_API_URL        e.g. https://your-api-url (no trailing slash)
//   BYDESIGN_CLIENT_ID
//   BYDESIGN_CLIENT_SECRET
//   BYDESIGN_API_KEY

declare(strict_types=1);

/** Minimal ByDesign API client that caches its access token and retries on 401 and 429. */
final class ByDesignClient
{
    private ?string $accessToken = null;
    private int $expiresAt = 0;

    public function __construct(
        private string $apiUrl,
        private string $clientId,
        private string $clientSecret,
        private string $apiKey,
        private string $scope = 'api/Read api/Write',
    ) {
        $this->apiUrl = rtrim($apiUrl, '/');
    }

    public function get(string $path): mixed
    {
        return $this->request('GET', $path);
    }

    public function post(string $path, array $body): mixed
    {
        return $this->request('POST', $path, $body);
    }

    public function request(string $method, string $path, ?array $body = null): mixed
    {
        for ($attempt = 1; ; $attempt++) {
            $headers = [
                'Authorization: Bearer ' . $this->getToken(),
                'x-api-key: ' . $this->apiKey,
            ];
            $payload = null;
            if ($body !== null) {
                $headers[] = 'Content-Type: application/json';
                $payload = json_encode($body, JSON_THROW_ON_ERROR);
            }

            [$status, $responseBody] = $this->send($method, $this->apiUrl . $path, $headers, $payload);

            if ($status === 401 && $attempt === 1) {
                $this->accessToken = null; // Token expired or was revoked: fetch a new one and retry once
                continue;
            }
            if ($status === 429 && $attempt <= 3) {
                sleep(2 ** $attempt);
                continue;
            }
            if ($status >= 400) {
                throw new RuntimeException("$method $path failed: $status $responseBody");
            }
            return $responseBody === '' ? null : json_decode($responseBody, true, 512, JSON_THROW_ON_ERROR);
        }
    }

    private function getToken(): string
    {
        // Reuse the token until one minute before it expires
        if ($this->accessToken !== null && time() < $this->expiresAt - 60) {
            return $this->accessToken;
        }

        [$status, $responseBody] = $this->send(
            'POST',
            $this->apiUrl . '/oauth2/token',
            ['Content-Type: application/x-www-form-urlencoded'],
            http_build_query([
                'grant_type' => 'client_credentials',
                'client_id' => $this->clientId,
                'client_secret' => $this->clientSecret,
                'scope' => $this->scope,
            ]),
        );
        if ($status !== 200) {
            throw new RuntimeException("Token request failed: $status $responseBody");
        }

        $token = json_decode($responseBody, true, 512, JSON_THROW_ON_ERROR);
        $this->accessToken = $token['access_token'];
        $this->expiresAt = time() + (int) $token['expires_in'];
        return $this->accessToken;
    }

    /** @return array{0: int, 1: string} */
    private function send(string $method, string $url, array $headers, ?string $body): array
    {
        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_CUSTOMREQUEST => $method,
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 30,
        ]);
        if ($body !== null) {
            curl_setopt($ch, CURLOPT_POSTFIELDS, $body);
        }

        $responseBody = curl_exec($ch);
        if ($responseBody === false) {
            $error = curl_error($ch);
            curl_close($ch);
            throw new RuntimeException("Request to $url failed: $error");
        }
        $status = (int) curl_getinfo($ch, CURLINFO_RESPONSE_CODE);
        curl_close($ch);
        return [$status, $responseBody];
    }
}

function required(string $name): string
{
    $value = getenv($name);
    if ($value === false || $value === '') {
        throw new RuntimeException("Set the $name environment variable.");
    }
    return $value;
}

$client = new ByDesignClient(
    required('BYDESIGN_API_URL'),
    required('BYDESIGN_CLIENT_ID'),
    required('BYDESIGN_CLIENT_SECRET'),
    required('BYDESIGN_API_KEY'),
);

// GET request: list inventory categories (requires api/Read)
print_r($client->get('/api/v2/inventory/categorydetail'));

// POST request: look up customers by email (requires api/Write)
print_r($client->post('/api/v2/customer/lookup', ['Email' => 'jane.doe@example.com']));
