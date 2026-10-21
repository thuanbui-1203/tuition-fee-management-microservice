import { Controller, Get } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';

@ApiTags('health')
@Controller('api/v1')
export class PingController {
  @Get('ping')
  ping() {
    return {
      service: 'payment-service',
      status: 'ok',
      time: new Date().toISOString(),
    };
  }
}
