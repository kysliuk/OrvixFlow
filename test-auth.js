const apiUrl = "http://localhost:8080";

async function test() {
  try {
    console.log("Provisioning OAuth user...");
    const res = await fetch(`${apiUrl}/api/auth/oauth-provision`, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ 
        email: "oauthuser@test.com", 
        displayName: "OAuth Node", 
        provider: "google", 
        externalId: "1234567890" 
      })
    });
    
    const rawText = await res.text();
    console.log("Raw Response Body:", rawText);
    
    let data;
    try {
      data = JSON.parse(rawText);
    } catch (e) {
      console.error("Failed to parse JSON:", rawText);
      return;
    }
    
    console.log("OAuth Token generated:", data.token ? "Success" : "Failed");
    
    console.log("Testing /api/agent/ingest endpoint...");
    const ingest = await fetch(`${apiUrl}/api/agent/ingest`, {
      method: "POST", headers: { "Content-Type": "application/json", "Authorization": "Bearer " + data.token },
      body: JSON.stringify({ prompt: "hello" })
    });
    
    console.log("Ingest Status:", ingest.status);
    console.log("Ingest Response:", await ingest.text());

  } catch (err) {
    console.error("Test script error:", err);
  }
}

test();
