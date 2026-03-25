const data = { email: 'test_node_3@example.com', password: 'password123', displayName: 'Test Node' };
fetch('http://localhost:5100/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
})
.then(r => r.text())
.then(console.log)
.catch(console.error);
