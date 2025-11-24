import React, { useState } from 'react';
import { api } from '../services/api';

export const ChatAssistant: React.FC = () => {
  const [message, setMessage] = useState('');
  const [reply, setReply] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const send = async () => {
    if (!message.trim()) return;
    setLoading(true);
    setReply(null);
    try {
      const res = await api.post<{ reply: string }>('/api/ai/chat', { message });
      setReply(res.data.reply);
    } catch (err) {
      setReply('Error contacting AI service. Check backend.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="chat-assistant">
      <textarea
        value={message}
        onChange={e => setMessage(e.target.value)}
        placeholder="Ask the AI assistant for product or shopping help…"
        rows={4}
      />
      <button onClick={send} disabled={loading}>
        {loading ? 'Thinking…' : 'Send'}
      </button>
      {reply && (
        <div className="chat-reply">
          <strong>AI reply:</strong>
          <p>{reply}</p>
        </div>
      )}
    </div>
  );
};
