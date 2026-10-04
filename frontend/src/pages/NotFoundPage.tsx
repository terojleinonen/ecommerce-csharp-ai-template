import { Link } from 'react-router';
import { EmptyState } from '../components/States';

export function NotFoundPage() {
  return (
    <EmptyState title="Page not found">
      <p>The page you're looking for doesn't exist.</p>
      <Link className="btn btn--primary" to="/">
        Back to the shop
      </Link>
    </EmptyState>
  );
}
