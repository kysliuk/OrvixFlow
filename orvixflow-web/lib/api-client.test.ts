import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { fetchApi } from './api-client';

// Mock the auth module
vi.mock('@/auth', () => ({
  auth: vi.fn(),
}));

import { auth } from '@/auth';

describe('fetchApi', () => {
  const originalEnv = process.env;

  beforeEach(() => {
    // Reset process.env and mocks
    process.env = { ...originalEnv };
    process.env.NEXT_PUBLIC_API_URL = 'http://api.example.com';
    vi.clearAllMocks();

    // Mock global fetch
    global.fetch = vi.fn();
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it('should call fetch with the correct endpoint, prefixing NEXT_PUBLIC_API_URL', async () => {
    // Setup
    (auth as any).mockResolvedValue(null);
    const mockResponse = new Response(JSON.stringify({ data: 'ok' }), { status: 200 });
    (global.fetch as any).mockResolvedValue(mockResponse);

    // Action
    await fetchApi('/test-endpoint');

    // Assert
    expect(global.fetch).toHaveBeenCalledWith(
      'http://api.example.com/test-endpoint',
      expect.any(Object)
    );
  });

  it('should append application/json Content-Type header by default', async () => {
    // Setup
    (auth as any).mockResolvedValue(null);
    const mockResponse = new Response(JSON.stringify({ data: 'ok' }), { status: 200 });
    (global.fetch as any).mockResolvedValue(mockResponse);

    // Action
    await fetchApi('/test');

    // Assert
    const fetchCallArgs = (global.fetch as any).mock.calls[0];
    const options = fetchCallArgs[1];
    expect(options.headers).toBeInstanceOf(Headers);
    expect(options.headers.get('Content-Type')).toBe('application/json');
  });

  it('should inject Authorization header when session contains apiToken', async () => {
    // Setup
    const token = 'test-token-123';
    (auth as any).mockResolvedValue({ apiToken: token });
    const mockResponse = new Response(JSON.stringify({ data: 'ok' }), { status: 200 });
    (global.fetch as any).mockResolvedValue(mockResponse);

    // Action
    await fetchApi('/test-secure');

    // Assert
    const fetchCallArgs = (global.fetch as any).mock.calls[0];
    const options = fetchCallArgs[1];
    expect(options.headers.get('Authorization')).toBe(`Bearer ${token}`);
  });

  it('should not inject Authorization header when session does not contain apiToken', async () => {
    // Setup
    (auth as any).mockResolvedValue({ user: { name: 'Test' } }); // session without apiToken
    const mockResponse = new Response(JSON.stringify({ data: 'ok' }), { status: 200 });
    (global.fetch as any).mockResolvedValue(mockResponse);

    // Action
    await fetchApi('/test-insecure');

    // Assert
    const fetchCallArgs = (global.fetch as any).mock.calls[0];
    const options = fetchCallArgs[1];
    expect(options.headers.has('Authorization')).toBe(false);
  });

  it('should pass custom options through to fetch', async () => {
    // Setup
    (auth as any).mockResolvedValue(null);
    const mockResponse = new Response(JSON.stringify({ data: 'ok' }), { status: 200 });
    (global.fetch as any).mockResolvedValue(mockResponse);

    // Action
    const customOptions = {
      method: 'POST',
      body: JSON.stringify({ name: 'test' }),
    };
    await fetchApi('/post-endpoint', customOptions);

    // Assert
    const fetchCallArgs = (global.fetch as any).mock.calls[0];
    const options = fetchCallArgs[1];
    expect(options.method).toBe('POST');
    expect(options.body).toBe(JSON.stringify({ name: 'test' }));
  });

  it('should return the response from fetch', async () => {
    // Setup
    (auth as any).mockResolvedValue(null);
    const mockResponse = new Response(JSON.stringify({ success: true }), { status: 200 });
    (global.fetch as any).mockResolvedValue(mockResponse);

    // Action
    const response = await fetchApi('/test');

    // Assert
    expect(response).toBe(mockResponse);
  });
});
