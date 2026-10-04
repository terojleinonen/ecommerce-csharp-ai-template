import { Link } from 'react-router';
import type { Product } from '../api/types';
import { useCart } from '../cart/context';
import { formatPrice } from '../lib/format';
import { ProductArt } from './ProductArt';
import { StockBadge } from './StockBadge';

export function ProductCard({ product }: { product: Product }) {
  const cart = useCart();
  const soldOut = product.stockQuantity <= 0;

  return (
    <article className="product-card">
      <Link to={`/products/${product.id}`} className="product-card__link">
        <ProductArt product={product} />
        <div className="product-card__body">
          <span className="product-card__category">{product.category}</span>
          <h3 className="product-card__name">{product.name}</h3>
          <p className="product-card__desc">{product.description}</p>
        </div>
      </Link>
      <div className="product-card__footer">
        <div>
          <strong className="price">{formatPrice(product.price)}</strong>
          <StockBadge stock={product.stockQuantity} />
        </div>
        <button
          type="button"
          className="btn btn--primary btn--sm"
          disabled={soldOut}
          onClick={() => cart.add(product)}
          aria-label={`Add ${product.name} to cart`}
        >
          {soldOut ? 'Sold out' : 'Add'}
        </button>
      </div>
    </article>
  );
}
