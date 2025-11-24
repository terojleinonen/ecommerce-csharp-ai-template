import axios from 'axios';

export const api = axios.create({
  baseURL: 'https://localhost:5001', // change if your backend runs elsewhere
});
