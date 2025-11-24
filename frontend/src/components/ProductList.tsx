import React, { useEffect, useState } from 'react';
import { api } from '../services/api';

export interface Product {
  id: number;
  sku: string;
  name: string;
  description?: string | null;
  price: number;
  imageUrl?: string | null;
  category?: string | null;
}

export const ProductList: React.FC = () => {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<Product[]>('/api/products')
      .then(res => setProducts(res.data))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <p>Loading products…</p>;

  return (
    <div className="product-grid">
      {products.map(p => (
        <article key={p.id} className="product-card">
          <div className="product-image-placeholder">
            {p.imageUrl ? <img src={p.imageUrl} alt={p.name} /> : <span>No image</span>}
          </div>
          <h3>{p.name}</h3>
          <p className="product-price">{p.price.toFixed(2)} €</p>
          <p className="product-description">{p.description ?? 'No description yet.'}</p>
        </article>
      ))}
    </div>
  );
};
