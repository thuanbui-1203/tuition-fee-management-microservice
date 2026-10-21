import { Module } from '@nestjs/common';
import { HttpModule } from '@nestjs/axios';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { UserClient } from './user.client';
import { TuitionClient } from './tuition.client';
import { OtpClient } from './otp.client';

@Module({
  imports: [
    HttpModule.registerAsync({
      imports: [ConfigModule],
      inject: [ConfigService],
      useFactory: (config: ConfigService) => ({
        // connect timeout cấp axios; read timeout set per-request trong client
        timeout: config.get<number>('HTTP_TIMEOUT_READ_MS') ?? 5000,
        maxRedirects: 0,
      }),
    }),
  ],
  providers: [UserClient, TuitionClient, OtpClient],
  exports: [UserClient, TuitionClient, OtpClient],
})
export class ClientsModule {}
