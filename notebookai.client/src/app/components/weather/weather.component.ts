import { Component, OnInit } from '@angular/core';
import { WeatherService } from '../../services/weather.service';
import { WeatherForecast, WeatherServiceResult } from '../../models/weather-forecast.interface';
import { Card } from "primeng/card";
import { Message } from "primeng/message";
import { TableModule } from "primeng/table";
import { Button } from "primeng/button";
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-weather',
  templateUrl: './weather.component.html',
  styleUrl: './weather.component.css',
  imports: [Card, Message, TableModule, Button, CommonModule]
})
export class WeatherComponent implements OnInit {
  weatherData: WeatherForecast[] = [];
  loading = false;
  error: string | null = null;

  constructor(private weatherService: WeatherService) {}

  ngOnInit() {
    this.loadWeatherData();
  }

  loadWeatherData() {
    this.loading = true;
    this.error = null;

    this.weatherService.getWeatherForecasts().subscribe({
      next: (result: WeatherServiceResult) => {
        this.loading = false;
        
        if (result.success) {
          this.weatherData = result.Data;
          this.error = null;
          console.log('Weather data loaded successfully:', result.message);
        } else {
          this.error = result.message || 'Failed to load weather data';
          this.weatherData = result.Data; // Still show mock data
          console.warn('Weather service returned error:', result.errors);
        }
      },
      error: (error) => {
        this.loading = false;
        this.error = 'Failed to load weather data';
        this.weatherData = [];
        console.error('Component error:', error);
      }
    });
  }

  // Keep for backward compatibility if used elsewhere
  getForecasts() {
    this.loadWeatherData();
  }
}
