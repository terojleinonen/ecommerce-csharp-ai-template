import React from 'react';
import { ProductList } from './components/ProductList';
import { ChatAssistant } from './components/ChatAssistant';
import './styles.css';

const App: React.FC = () => {
  return (
    <div className="app-root">
      <header className="app-header">
        <h1>E‑Commerce React + C# AI Template</h1>
        <p>Demo store with AI‑powered features (stubbed).</p>
      </header>
      <main className="app-main">
        <section>
          <h2>Products</h2>
          <ProductList />
        </section>
        <section>
          <h2>AI Shopping Assistant</h2>
          <ChatAssistant />
        </section>
      </main>
    </div>
  );
};

export default App;
