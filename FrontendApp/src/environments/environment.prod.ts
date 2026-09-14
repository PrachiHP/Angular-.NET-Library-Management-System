// Production: the API is expected behind the same origin as the app, so
// relative URLs avoid CORS entirely and survive a domain change.
export const environment = {
  production: true,
  apiUrl: '/api',
  fileOrigin: '',
};
