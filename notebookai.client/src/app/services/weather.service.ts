import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, of, map } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '@auth0/auth0-angular';
import { WeatherForecast, ServiceResult } from '../models/weather-forecast.interface';

// Type alias for cleaner code
export type WeatherServiceResult = ServiceResult<WeatherForecast[]>;
export type SingleWeatherResult = ServiceResult<WeatherForecast>;

@Injectable({
  providedIn: 'root'
})
export class WeatherService {
  private apiUrl = this.getApiUrl();

  constructor(
    private http: HttpClient,
    private auth: AuthService
  ) {
    console.log('WeatherService - Final API URL:', this.apiUrl);
    console.log('Process env API_URL:', process.env['API_URL']);
    console.log('Environment apiUrl:', environment.apiUrl);
    console.log('Environment production:', environment.production);
    console.log('Environment useProxy:', environment.useProxy);
    console.log('Auth0 audience:', environment.auth0.audience);
  }

  private getApiUrl(): string {
    // First priority: Environment variable from .env file (injected by webpack)
    if (process.env['API_URL']) {
      console.log('Using API_URL from environment variable:', process.env['API_URL']);
      return process.env['API_URL'];
    }
    
    // Second priority: Angular environment file
    if (environment.apiUrl) {
      console.log('Using apiUrl from Angular environment:', environment.apiUrl);
      return environment.apiUrl;
    }
    
    // Fallback: Default based on production mode
    const fallbackUrl = environment.production 
      ? 'https://api.global-webnet.com' 
      : 'https://localhost:7280';
    console.warn('No API URL configured, using fallback:', fallbackUrl);
    return fallbackUrl;
  }

  getWeatherForecasts(): Observable<WeatherServiceResult> {
    const url = this.apiUrl ? `${this.apiUrl}/weatherforecast` : '/weatherforecast';
    console.log('Making request to:', url);
    console.log('Auth0 interceptor will automatically add Bearer token if user is authenticated');
    
    return this.http.get<WeatherForecast[]>(url).pipe(
      map((data: WeatherForecast[]) => {
        // Transform successful response into WeatherServiceResult
        return this.createSuccessResponse(data);
      }),
      catchError((error: HttpErrorResponse) => {
        console.error('API Error Details:', {
          status: error.status,
          statusText: error.statusText,
          message: error.message,
          url: error.url,
          headers: error.headers?.keys()
        });
        
        let errorMessage = 'Unknown error occurred';
        const errors: string[] = [];
        
        if (error.status === 401) {
          console.error('401 Unauthorized - Check server-side Auth0 configuration');
          console.log('Server might need to be configured to accept Auth0 tokens');
          console.log('Audience should match:', environment.auth0.audience);
          errorMessage = 'Authentication required. Please log in.';
          errors.push('Invalid or missing authentication token');
        }
        
        if (error.status === 0) {
          console.warn('Network error - Backend might not be running or CORS issue');
          console.log('Check if your ASP.NET backend is running on:', this.apiUrl);
          errorMessage = 'Network error. Please check your connection.';
          errors.push('Unable to connect to server');
        }
        
        if (error.status >= 500) {
          errorMessage = 'Server error. Please try again later.';
          errors.push('Internal server error');
        }
        
        // Return standardized error response with mock data
        console.warn(`Using mock data due to API error: ${error.status} ${error.statusText}`);
        return of(this.createErrorResponse(errorMessage, errors));
      })
    );
  }

  private createErrorResponse(message: string, errors: string[]): WeatherServiceResult {
    return {
      Data: this.getMockWeatherData(),
      success: false,
      message,
      errors
    };
  }

  private createSuccessResponse(data: WeatherForecast[]): WeatherServiceResult {
    return {
      Data: data,
      success: true,
      message: 'Weather data retrieved successfully'
    };
  }

  getMockWeatherData(): WeatherForecast[] {
    return [
      {
        date: new Date().toISOString(),
        temperatureC: 22,
        temperatureF: 71.6,
        summary: 'Mock Sunny Data'
      },
      {
        date: new Date(Date.now() + 86400000).toISOString(),
        temperatureC: 18,
        temperatureF: 64.4,
        summary: 'Mock Cloudy Data'
      },
      {
        date: new Date(Date.now() + 2 * 86400000).toISOString(),
        temperatureC: 15,
        temperatureF: 59,
        summary: 'Mock Rainy Data'
      }
    ];
  }
}
