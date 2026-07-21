module.exports = {
	default: {
		import: ['features/support/**/*.ts'],
		paths: ['features/**/*.feature'],
		tags: 'not @api and not @ignore',
		format: ['progress-bar'],
		worldParameters: {
			appUrl: 'http://localhost:5173',
			apiUrl: 'http://localhost:80/api/',
		},
		formatOptions: {
			snippetInterface: 'async-await',
		},
		timeout: 30000,
	},
};
