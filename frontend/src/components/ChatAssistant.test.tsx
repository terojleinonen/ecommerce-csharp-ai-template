import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { jsonResponse, mockFetch, product, renderWithProviders } from '../test/utils';
import { ChatAssistant } from './ChatAssistant';

describe('ChatAssistant', () => {
  it('sends the conversation and renders the reply with product suggestions', async () => {
    const fetchMock = mockFetch({
      '/api/assistant/status': () => jsonResponse({ provider: 'claude', llmEnabled: true, model: 'claude-opus-5-5' }),
      '/api/assistant/chat': () =>
        jsonResponse({ reply: 'The Trail Runner GTX is a great waterproof pick.', products: [product()], provider: 'claude' }),
    });
    const user = userEvent.setup();
    renderWithProviders(<ChatAssistant />);

    await user.click(screen.getByRole('button', { name: /open shopping assistant/i }));
    expect(await screen.findByText('Claude')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Message'), 'waterproof shoes');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText(/great waterproof pick/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Trail Runner GTX' })).toHaveAttribute('href', '/products/1');

    const chatCall = fetchMock.mock.calls.find(([url]) => String(url).includes('/api/assistant/chat'))!;
    expect(JSON.parse(chatCall[1]!.body as string)).toEqual({
      messages: [{ role: 'user', content: 'waterproof shoes' }],
    });
  });

  it('shows rate limit errors without sending them back to the model', async () => {
    const fetchMock = mockFetch({
      '/api/assistant/status': () => jsonResponse({ provider: 'offline', llmEnabled: false, model: null }),
      '/api/assistant/chat': () => jsonResponse({}, 429),
    });
    const user = userEvent.setup();
    renderWithProviders(<ChatAssistant />);

    await user.click(screen.getByRole('button', { name: /open shopping assistant/i }));
    await user.click(await screen.findByRole('button', { name: /gift under €60/i }));
    expect(await screen.findByText(/too many requests/i)).toBeInTheDocument();

    await user.type(screen.getByLabelText('Message'), 'hello again');
    await user.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => {
      const chatCalls = fetchMock.mock.calls.filter(([url]) => String(url).includes('/api/assistant/chat'));
      expect(chatCalls).toHaveLength(2);
      const body = JSON.parse(chatCalls[1]![1]!.body as string) as { messages: { role: string }[] };
      expect(body.messages.map((m) => m.role)).toEqual(['user', 'user']);
    });
  });
});
