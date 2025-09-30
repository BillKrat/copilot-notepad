export interface IServiceData {
  id?: string;
  timestamp?: Date;
  status?: 'success' | 'error' | 'pending';
}

export interface ServiceResult<T = any> {
  Data: T;
  success: boolean;
  message?: string;
  errors?: string[];
}

export interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

// Type aliases for common use cases
export type WeatherServiceResult = ServiceResult<WeatherForecast[]>;
export type SingleWeatherResult = ServiceResult<WeatherForecast>;
export type WeatherData = WeatherForecast & IServiceData;

