// Development settings. Swapped for environment.prod.ts at production build
// time via the "fileReplacements" entry in angular.json.
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5041/api',
  // Origin the API serves uploaded files from. Separate from apiUrl because
  // cover and quote images live at /uploads/..., outside the /api prefix.
  fileOrigin: 'http://localhost:5041',
};
