import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '../api/endpoints';
import type { ChatMessage, Product } from '../api/types';
import { useCart } from '../cart/context';
import { formatPrice, readStorage, writeStorage } from '../lib/format';

interface Turn extends ChatMessage {
  products?: Product[];
  error?: boolean;
}

const STORAGE_KEY = 'shopsense.chat';
const SUGGESTIONS = ['Waterproof shoes for autumn trails', 'A gift under €60 for a coffee lover', 'What do I need for a weekend camping trip?'];
const GREETING: Turn = {
  role: 'assistant',
  content: "Hi! I'm your shopping assistant. Tell me what you're looking for and I'll find it in our catalog.",
};

export function ChatAssistant() {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const [turns, setTurns] = useState<Turn[]>(() => readStorage<Turn[]>(sessionStorage, STORAGE_KEY, []));
  const listRef = useRef<HTMLDivElement>(null);
  const cart = useCart();

  const status = useQuery({ queryKey: ['assistant-status'], queryFn: api.assistantStatus, staleTime: Infinity, enabled: open });

  const chat = useMutation({
    mutationFn: (history: ChatMessage[]) => api.chat(history),
    onSuccess: (reply) =>
      setTurns((t) => [...t, { role: 'assistant', content: reply.reply, products: reply.products }]),
    onError: (err) =>
      setTurns((t) => [
        ...t,
        { role: 'assistant', content: err instanceof Error ? err.message : 'Something went wrong.', error: true },
      ]),
  });

  useEffect(() => writeStorage(sessionStorage, STORAGE_KEY, turns), [turns]);

  useEffect(() => {
    const el = listRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [turns, chat.isPending, open]);

  const send = (text: string) => {
    const content = text.trim();
    if (!content || chat.isPending) return;
    const next: Turn[] = [...turns, { role: 'user', content }];
    setTurns(next);
    setInput('');
    // Error turns are UI-only; don't send them to the model.
    chat.mutate(next.filter((t) => !t.error).map(({ role, content: c }) => ({ role, content: c })));
  };

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    send(input);
  };

  const visible = turns.length === 0 ? [GREETING] : turns;

  return (
    <>
      <button
        type="button"
        className="chat-fab"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        aria-controls="chat-panel"
        aria-label={open ? 'Close shopping assistant' : 'Open shopping assistant'}
      >
        {open ? '✕' : '✦ Ask AI'}
      </button>

      {open && (
        <section id="chat-panel" className="chat" aria-label="Shopping assistant">
          <header className="chat__header">
            <div>
              <strong>Shopping assistant</strong>
              {status.data && (
                <span className={`badge ${status.data.llmEnabled ? 'badge--ai' : ''}`}>
                  {status.data.llmEnabled ? 'Claude' : 'Offline demo'}
                </span>
              )}
            </div>
            {turns.length > 0 && (
              <button type="button" className="link-button" onClick={() => setTurns([])}>
                New chat
              </button>
            )}
          </header>

          <div className="chat__messages" ref={listRef} aria-live="polite">
            {visible.map((turn, i) => (
              <div key={i} className={`chat__msg chat__msg--${turn.role} ${turn.error ? 'chat__msg--error' : ''}`}>
                <p>{turn.content}</p>
                {turn.products && turn.products.length > 0 && (
                  <ul className="chat__products">
                    {turn.products.map((p) => (
                      <li key={p.id}>
                        <Link to={`/products/${p.id}`}>{p.name}</Link>
                        <span>{formatPrice(p.price)}</span>
                        <button
                          type="button"
                          className="btn btn--sm btn--ghost"
                          disabled={p.stockQuantity <= 0}
                          onClick={() => cart.add(p)}
                          aria-label={`Add ${p.name} to cart`}
                        >
                          + Cart
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
            {chat.isPending && (
              <div className="chat__msg chat__msg--assistant">
                <span className="typing" aria-label="Assistant is typing">
                  <i />
                  <i />
                  <i />
                </span>
              </div>
            )}
          </div>

          {turns.length === 0 && (
            <div className="chat__suggestions">
              {SUGGESTIONS.map((s) => (
                <button key={s} type="button" className="chip" onClick={() => send(s)}>
                  {s}
                </button>
              ))}
            </div>
          )}

          <form className="chat__form" onSubmit={onSubmit}>
            <input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Ask about products, sizes, gifts…"
              aria-label="Message"
              maxLength={2000}
            />
            <button type="submit" className="btn btn--primary" disabled={chat.isPending || !input.trim()}>
              Send
            </button>
          </form>
        </section>
      )}
    </>
  );
}
