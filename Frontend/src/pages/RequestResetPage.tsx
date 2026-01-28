import { useState } from 'react';
import { Link } from 'react-router-dom';
import { authAPI } from '../services/api';

export const RequestResetPage = () => {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await authAPI.requestPasswordReset(email);
      setSuccess(true);
    } catch (err: any) {
      setError(err.response?.data?.error || 'Błąd połączenia');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <h1 style={{ marginBottom: '30px' }}>Reset hasła</h1>
      
      {error && <div className="error">{error}</div>}
      {success && (
        <div className="success">
          Jeśli konto z tym adresem email istnieje, link do resetowania hasła został wysłany.
        </div>
      )}
      
      {!success && (
        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              disabled={loading}
            />
          </div>
          
          <button type="submit" disabled={loading}>
            {loading ? 'Wysyłanie...' : 'Wyślij link resetujący'}
          </button>
          
          <div style={{ marginTop: '20px', textAlign: 'center' }}>
            <Link to="/login" className="link">Powrót do logowania</Link>
          </div>
        </form>
      )}
      
      {success && (
        <div style={{ marginTop: '20px', textAlign: 'center' }}>
          <Link to="/login" className="link">Powrót do logowania</Link>
        </div>
      )}
    </div>
  );
};
