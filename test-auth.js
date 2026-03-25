const apiUrl = "http://localhost:5100";

async function test() {
  try {
    console.info("Provisioning OAuth user...");
    const res = await fetch(`${apiUrl}/api/auth/oauth-provision`, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ 
        email: "oauthuser@test.com", 
        displayName: "OAuth Node", 
        provider: "google", 
        externalId: "1234567890" 
      })
    });
    
    const data = await res.json();
    console.info("OAuth Token generated:", data.token ? "Success" : "Failed");
    if (!res.ok) {
       console.error("Error response:", data);
    }
    
    console.info("Testing /api/agent/ingest endpoint...");
    const ingest = await fetch(`${apiUrl}/api/agent/ingest`, {
      method: "POST", headers: { "Content-Type": "application/json", "Authorization": "Bearer " + data.token },
      body: JSON.stringify({ prompt: "hello" })
    });
    
    console.info("Ingest Status:", ingest.status);

  } catch (err) {
    console.error("Test script error:", err);
  }
}

test();
